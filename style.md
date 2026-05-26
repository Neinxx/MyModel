# Unity Editor UI Style Guide & Design Token System (style.md)

This style guide documents the project's **Premium, High-Contrast Industrial-Grade Editor UI Aesthetic**. Use this document as the design system reference whenever creating or modifying custom Unity Editor Windows, Custom Inspectors, or Property Drawers to ensure visual harmony, readability, and a highly polished professional feel.

---

## 🎨 Color Palette & Design Tokens

Avoid raw primary colors (e.g., plain `Color.red` or `Color.green`). Use the following custom-blended, highly saturated, yet dark-mode friendly professional HSL-tailored colors.

| Token / Role | Unity Color Representation | Hex Equivalent | Description & Context |
| :--- | :--- | :--- | :--- |
| **Primary Azure / Accent** | `new Color(0.24f, 0.72f, 1.00f)` | `#3DB8FF` | Used for Main Title, active tabs, and primary accents. |
| **Success Emerald Green** | `new Color(0.25f, 0.72f, 0.31f)` | `#40B84F` | High-contrast saturated green for key success buttons and actions. |
| **Danger / Wine Red** | `new Color(0.53f, 0.21f, 0.21f)` | `#873535` | Background color for failed states, block warnings, and alerts. |
| **Deep Forest Green** | `new Color(0.18f, 0.39f, 0.22f)` | `#2E6338` | Background color for successful checklist / passed alert boxes. |
| **Success Light Mint Text** | `new Color(0.60f, 1.00f, 0.67f)` | `#99FFAA` | Foreground text color used in green success banners. |
| **Danger Pastel Red Text** | `new Color(1.00f, 0.67f, 0.67f)` | `#FFAAAA` | Foreground text color used in wine red failed/error banners. |
| **Muted Grey Text** | `Color.gray` or `new Color(0.9f, 0.9f, 0.9f)` | `#E5E5E5` | Used for descriptions, subtitles, and secondary info. |
| **Separator Divider Line** | `new Color(0.30f, 0.30f, 0.30f, 0.50f)`| `#4D4D4D80` | Perfect high-contrast subtle dark divider color. |

---

## 📐 Typography & Font Styles

Always define explicit `GUIStyle` states (normal text colors, sizes, alignment) inside custom editors to maintain font hierarchy:

### 1. Main Window Title Header
* **Font Size**: `22` (Bold)
* **Alignment**: `TextAnchor.MiddleCenter`
* **Color**: Primary Azure (`#3DB8FF`)
```csharp
var titleStyle = new GUIStyle(EditorStyles.boldLabel)
{
    fontSize = 22,
    alignment = TextAnchor.MiddleCenter
};
titleStyle.normal.textColor = new Color(0.24f, 0.72f, 1.00f);
```

### 2. Window Subtitle
* **Font Size**: `12` (Normal)
* **Alignment**: `TextAnchor.MiddleCenter`
* **Color**: `Color.gray`
```csharp
var subtitleStyle = new GUIStyle(EditorStyles.label)
{
    fontSize = 12,
    alignment = TextAnchor.MiddleCenter
};
subtitleStyle.normal.textColor = Color.gray;
```

### 3. Section Header
* **Font Size**: `14` (Bold)
* **Margin**: `new RectOffset(0, 0, 10, 5)`
* **Color**: Default Unity Editor Label (High contrast white/light grey)
```csharp
var sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
{
    fontSize = 14,
    margin = new RectOffset(0, 0, 10, 5)
};
```

---

## 🔘 Buttons & Clickable Elements

Make active buttons highly clickable and tactile by defining appropriate height, margins, and custom backgrounds where relevant.

### 1. Large Tactile Call-To-Action (CTA) Button
* **Height**: `40px`
* **Font Size**: `13` (Bold)
* **Background Color**: Saturated Emerald Green (`#40B84F`)
* **Reset Pattern**: Always reset `GUI.backgroundColor` to `Color.white` immediately after drawing to avoid coloring subsequent native fields!
```csharp
var buttonStyle = new GUIStyle(GUI.skin.button)
{
    fontSize = 13,
    fontStyle = FontStyle.Bold,
    fixedHeight = 40
};

GUI.backgroundColor = new Color(0.25f, 0.72f, 0.31f); // Emerald green
if (GUILayout.Button(" ONE-CLICK SMART ACTION ", buttonStyle))
{
    // Execution
}
GUI.backgroundColor = Color.white; // Critical reset
```

### 2. Double-Column Auxiliary Button Layout
Wrap multiple secondary buttons in horizontal layouts for dense layout efficiency.
* **Height**: `30px`
```csharp
GUILayout.BeginHorizontal();
if (GUILayout.Button("Action A", GUILayout.Height(30))) { /* ... */ }
if (GUILayout.Button("Action B", GUILayout.Height(30))) { /* ... */ }
GUILayout.EndHorizontal();
```

---

## 🖼️ Panels, Cards & Custom Banners

Checklists, status reports, and key parameters should be contained inside styled panels/cards with explicit padding and clear icons rather than generic labels.

### 1. Micro-Status Notification Banner (Pass / Fail Cards)
* **Layout**: `BeginVertical` with a padded `boxStyle`
* **Padding**: `new RectOffset(10, 10, 8, 8)`
* **Margin**: `new RectOffset(5, 5, 5, 5)`
* **Styles**:
  * Pass State: Icon `✔`, Background `#2E6338`, Text `#99FFAA`
  * Fail State: Icon `✘`, Background `#873535`, Text `#FFAAAA`

```csharp
private void DrawAuditBox(bool passed, string title, string message)
{
    Color bgColor = passed ? new Color(0.18f, 0.39f, 0.22f) : new Color(0.53f, 0.21f, 0.21f);
    Color textColor = passed ? new Color(0.60f, 1.00f, 0.67f) : new Color(1.00f, 0.67f, 0.67f);

    var boxStyle = new GUIStyle(GUI.skin.box)
    {
        margin = new RectOffset(5, 5, 5, 5),
        padding = new RectOffset(10, 10, 8, 8)
    };

    GUI.backgroundColor = bgColor;
    GUILayout.BeginVertical(boxStyle);
    GUI.backgroundColor = Color.white; // Reset background

    var headerStyle = new GUIStyle(EditorStyles.boldLabel);
    headerStyle.normal.textColor = textColor;
    GUILayout.Label($"{(passed ? "✔" : "✘")}  {title}", headerStyle);

    var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
    descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
    GUILayout.Label(message, descStyle);

    GUILayout.EndVertical();
}
```

---

## 📏 Layout Guidelines & Separation

* **Dividers**: Draw clean horizontal lines to demarcate major configuration categories.
```csharp
private void DrawLine()
{
    Rect rect = EditorGUILayout.GetControlRect(false, 1);
    rect.height = 1;
    EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
    GUILayout.Space(5);
}
```
* **Scroll Views**: Constrain checklist displays inside predefined `ScrollView` windows to avoid infinite vertical layout stretching.
* **Window Size Limit**: Set proper `minSize` (e.g., `new Vector2(500, 600)`) on custom `EditorWindow` subclasses to avoid squishing UI elements.
