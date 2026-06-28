# FacefusionMac

一个面向 macOS 的实时换脸 MVP。它使用 OpenCV 从摄像头读取画面，检测目标人脸，并把你提供的源脸图片实时融合到摄像头人脸区域。

> 请只对自己或已明确同意的人使用本工具。实时换脸很容易被误用，生成内容请主动标注为合成。

## DeepFaceLive 上游

你指定的 GitHub 项目是：

`https://github.com/iperov/DeepFaceLive`

DeepFaceLive 官方仓库已归档，官方发布版主要面向 Windows 10 + DirectX 12 显卡。macOS 不能直接按官方发布包运行，所以本项目采用两层结构：

- `app.py`：Mac 可运行的实时摄像头换脸 MVP。
- `deepfacelive_bridge.py`：DeepFaceLive 上游仓库获取/检测脚本，用作参考后端或 Windows 路径。

查看当前机器和 DeepFaceLive 状态：

```bash
python deepfacelive_bridge.py status
```

拉取 DeepFaceLive 官方仓库：

```bash
python deepfacelive_bridge.py fetch
```

如果 GitHub 网络很慢，可以稍后重复运行同一条命令。

## 运行环境

- macOS
- Python 3.9+
- 摄像头权限

## 安装

```bash
cd /Users/neinxx/PrejectQYQY/FacefusionMac
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

第一次打开摄像头时，macOS 可能会弹出权限请求。如果没有弹窗，可以到：

`系统设置 -> 隐私与安全性 -> 摄像头`

允许 Terminal、iTerm、Codex 或你运行脚本的应用访问摄像头。

## 使用

准备一张正脸照片，例如：

```bash
python app.py --source /absolute/path/to/source_face.jpg
```

如果你的 Mac 有多个摄像头：

```bash
python app.py --source /absolute/path/to/source_face.jpg --camera 1
```

## 热键

- `q` 或 `Esc`：退出
- `r`：重新读取源脸图片
- `b`：切换融合模式
- `[` / `]`：降低/提高融合强度
- `-` / `=`：缩小/放大融合区域

## 当前版本的边界

这是轻量实时 MVP，优点是依赖少、容易在 Mac 上跑起来；缺点是没有身份级深度模型，侧脸、遮挡、强光和大幅表情变化时效果会下降。下一步可以接入 ONNX Runtime 或 Core ML，把 `FaceSwapper` 后端替换成模型推理。
