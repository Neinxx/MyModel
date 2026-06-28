from __future__ import annotations

import argparse
import time
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np


WINDOW_NAME = "FacefusionMac - realtime face swap"


@dataclass
class RuntimeOptions:
    source: Path
    camera: int
    width: int
    strength: float
    scale: float


class FaceDetector:
    def __init__(self) -> None:
        cascade_path = Path(cv2.data.haarcascades) / "haarcascade_frontalface_default.xml"
        self.classifier = cv2.CascadeClassifier(str(cascade_path))
        if self.classifier.empty():
            raise RuntimeError(f"Could not load face cascade: {cascade_path}")

    def detect(self, frame: np.ndarray) -> list[tuple[int, int, int, int]]:
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        gray = cv2.equalizeHist(gray)
        faces = self.classifier.detectMultiScale(
            gray,
            scaleFactor=1.08,
            minNeighbors=5,
            minSize=(80, 80),
            flags=cv2.CASCADE_SCALE_IMAGE,
        )
        return sorted((tuple(map(int, face)) for face in faces), key=lambda item: item[2] * item[3], reverse=True)


class FaceSwapper:
    def __init__(self, source_path: Path, strength: float, scale: float) -> None:
        self.source_path = source_path
        self.strength = strength
        self.scale = scale
        self.blend_mode = 0
        self.source = self._load_source()

    def reload(self) -> None:
        self.source = self._load_source()

    def _load_source(self) -> np.ndarray:
        image = cv2.imread(str(self.source_path), cv2.IMREAD_COLOR)
        if image is None:
            raise FileNotFoundError(f"Could not read source image: {self.source_path}")
        return self._crop_largest_source_face(image)

    @staticmethod
    def _crop_largest_source_face(image: np.ndarray) -> np.ndarray:
        cascade_path = Path(cv2.data.haarcascades) / "haarcascade_frontalface_default.xml"
        classifier = cv2.CascadeClassifier(str(cascade_path))
        if classifier.empty():
            return image

        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        faces = classifier.detectMultiScale(
            cv2.equalizeHist(gray),
            scaleFactor=1.08,
            minNeighbors=5,
            minSize=(80, 80),
            flags=cv2.CASCADE_SCALE_IMAGE,
        )
        if len(faces) == 0:
            return image

        x, y, w, h = max((tuple(map(int, face)) for face in faces), key=lambda item: item[2] * item[3])
        pad_x = int(w * 0.36)
        pad_y_top = int(h * 0.48)
        pad_y_bottom = int(h * 0.30)
        x0 = max(0, x - pad_x)
        y0 = max(0, y - pad_y_top)
        x1 = min(image.shape[1], x + w + pad_x)
        y1 = min(image.shape[0], y + h + pad_y_bottom)
        return image[y0:y1, x0:x1]

    def swap(self, frame: np.ndarray, face: tuple[int, int, int, int]) -> np.ndarray:
        x, y, w, h = self._expanded_face_rect(frame, face)
        if w <= 10 or h <= 10:
            return frame

        target_roi = frame[y : y + h, x : x + w]
        source_face = self._prepare_source_face((w, h), target_roi)
        mask = self._make_face_mask(w, h)
        center = (x + w // 2, y + h // 2)

        if self.blend_mode == 0:
            try:
                blended = cv2.seamlessClone(source_face, frame, mask, center, cv2.NORMAL_CLONE)
            except cv2.error:
                blended = self._alpha_blend(frame, source_face, mask, x, y)
        else:
            blended = self._alpha_blend(frame, source_face, mask, x, y)

        if self.strength >= 0.99:
            return blended
        return cv2.addWeighted(blended, self.strength, frame, 1.0 - self.strength, 0)

    def _expanded_face_rect(
        self, frame: np.ndarray, face: tuple[int, int, int, int]
    ) -> tuple[int, int, int, int]:
        x, y, w, h = face
        cx = x + w / 2
        cy = y + h / 2
        new_w = w * self.scale
        new_h = h * self.scale
        x0 = max(0, int(cx - new_w / 2))
        y0 = max(0, int(cy - new_h / 2))
        x1 = min(frame.shape[1], int(cx + new_w / 2))
        y1 = min(frame.shape[0], int(cy + new_h / 2))
        return x0, y0, x1 - x0, y1 - y0

    def _prepare_source_face(self, size: tuple[int, int], target_roi: np.ndarray) -> np.ndarray:
        w, h = size
        resized = cv2.resize(self.source, (w, h), interpolation=cv2.INTER_AREA)
        return self._match_color(resized, target_roi)

    @staticmethod
    def _make_face_mask(w: int, h: int) -> np.ndarray:
        mask = np.zeros((h, w), dtype=np.uint8)
        center = (w // 2, h // 2)
        axes = (max(1, int(w * 0.43)), max(1, int(h * 0.48)))
        cv2.ellipse(mask, center, axes, 0, 0, 360, 255, -1)
        mask = cv2.GaussianBlur(mask, (31, 31), 0)
        return mask

    @staticmethod
    def _match_color(source: np.ndarray, target: np.ndarray) -> np.ndarray:
        src_lab = cv2.cvtColor(source, cv2.COLOR_BGR2LAB).astype(np.float32)
        tgt_lab = cv2.cvtColor(target, cv2.COLOR_BGR2LAB).astype(np.float32)

        src_mean, src_std = cv2.meanStdDev(src_lab)
        tgt_mean, tgt_std = cv2.meanStdDev(tgt_lab)
        src_mean = src_mean.reshape((1, 1, 3))
        src_std = src_std.reshape((1, 1, 3))
        tgt_mean = tgt_mean.reshape((1, 1, 3))
        tgt_std = tgt_std.reshape((1, 1, 3))

        corrected = (src_lab - src_mean) * (tgt_std / np.maximum(src_std, 1.0)) + tgt_mean
        corrected = np.clip(corrected, 0, 255).astype(np.uint8)
        return cv2.cvtColor(corrected, cv2.COLOR_LAB2BGR)

    @staticmethod
    def _alpha_blend(frame: np.ndarray, source_face: np.ndarray, mask: np.ndarray, x: int, y: int) -> np.ndarray:
        output = frame.copy()
        h, w = mask.shape
        alpha = (mask.astype(np.float32) / 255.0)[:, :, None]
        roi = output[y : y + h, x : x + w].astype(np.float32)
        mixed = source_face.astype(np.float32) * alpha + roi * (1.0 - alpha)
        output[y : y + h, x : x + w] = np.clip(mixed, 0, 255).astype(np.uint8)
        return output


class FpsCounter:
    def __init__(self) -> None:
        self.last = time.perf_counter()
        self.value = 0.0

    def tick(self) -> float:
        now = time.perf_counter()
        delta = now - self.last
        self.last = now
        if delta > 0:
            current = 1.0 / delta
            self.value = current if self.value == 0 else self.value * 0.9 + current * 0.1
        return self.value


def parse_args() -> RuntimeOptions:
    parser = argparse.ArgumentParser(description="Realtime local face swap MVP for macOS.")
    parser.add_argument("--source", required=True, type=Path, help="Path to the source face image.")
    parser.add_argument("--camera", type=int, default=0, help="OpenCV camera index.")
    parser.add_argument("--width", type=int, default=960, help="Preview width.")
    parser.add_argument("--strength", type=float, default=0.86, help="Blend strength, 0.0 to 1.0.")
    parser.add_argument("--scale", type=float, default=1.22, help="Face region scale.")
    args = parser.parse_args()
    return RuntimeOptions(
        source=args.source.expanduser().resolve(),
        camera=args.camera,
        width=max(320, args.width),
        strength=float(np.clip(args.strength, 0.05, 1.0)),
        scale=float(np.clip(args.scale, 0.75, 1.8)),
    )


def draw_hud(frame: np.ndarray, fps: float, swapper: FaceSwapper, face_count: int) -> None:
    lines = [
        f"FPS {fps:4.1f} | faces {face_count}",
        f"strength {swapper.strength:.2f} | scale {swapper.scale:.2f} | mode {'clone' if swapper.blend_mode == 0 else 'alpha'}",
        "q/esc quit  r reload  b mode  [ ] strength  - = scale",
    ]
    for index, text in enumerate(lines):
        y = 28 + index * 26
        cv2.putText(frame, text, (16, y), cv2.FONT_HERSHEY_SIMPLEX, 0.62, (0, 0, 0), 4, cv2.LINE_AA)
        cv2.putText(frame, text, (16, y), cv2.FONT_HERSHEY_SIMPLEX, 0.62, (245, 245, 245), 1, cv2.LINE_AA)


def resize_frame(frame: np.ndarray, width: int) -> np.ndarray:
    if frame.shape[1] <= width:
        return frame
    scale = width / frame.shape[1]
    height = int(frame.shape[0] * scale)
    return cv2.resize(frame, (width, height), interpolation=cv2.INTER_AREA)


def handle_key(key: int, swapper: FaceSwapper) -> bool:
    if key in (27, ord("q")):
        return False
    if key == ord("r"):
        swapper.reload()
    elif key == ord("b"):
        swapper.blend_mode = 1 - swapper.blend_mode
    elif key == ord("["):
        swapper.strength = max(0.05, swapper.strength - 0.04)
    elif key == ord("]"):
        swapper.strength = min(1.0, swapper.strength + 0.04)
    elif key == ord("-"):
        swapper.scale = max(0.75, swapper.scale - 0.04)
    elif key in (ord("="), ord("+")):
        swapper.scale = min(1.8, swapper.scale + 0.04)
    return True


def main() -> int:
    options = parse_args()
    detector = FaceDetector()
    swapper = FaceSwapper(options.source, options.strength, options.scale)
    fps = FpsCounter()

    capture = cv2.VideoCapture(options.camera)
    if not capture.isOpened():
        raise RuntimeError(f"Could not open camera index {options.camera}")

    capture.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

    cv2.namedWindow(WINDOW_NAME, cv2.WINDOW_NORMAL)

    try:
        while True:
            ok, frame = capture.read()
            if not ok:
                raise RuntimeError("Camera returned no frame.")

            frame = resize_frame(frame, options.width)
            faces = detector.detect(frame)
            output = frame
            if faces:
                output = swapper.swap(frame, faces[0])

            draw_hud(output, fps.tick(), swapper, len(faces))
            cv2.imshow(WINDOW_NAME, output)

            key = cv2.waitKey(1) & 0xFF
            if not handle_key(key, swapper):
                break
    finally:
        capture.release()
        cv2.destroyAllWindows()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
