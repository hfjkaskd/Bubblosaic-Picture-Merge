using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class FAQAccordionAuthoring
    {
        const string PrefabPath = "Assets/BizzaWZ/Final/Real/UI/FAQPanel/FAQPanel.prefab";
        const string CircleMaskPath = "Assets/BubblePics/RuntimeArt/FAQCircleMask.png";
        const float S = 2360f / 1846f;

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring FAQ accordions.");

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(GameObject root)
        {
            var scroll = root.GetComponentInChildren<ScrollRect>(true);
            var content = scroll.content;
            var sample = content.Find("QuickQuestion1/Question").GetComponent<TextMeshProUGUI>();
            var sampleIcon = FindIcon(content.Find("QuickQuestion1")).gameObject;

            for (int i = 1; i <= 4; i++)
            {
                var row = content.Find("QuickQuestion" + i);
                var question = row.Find("Question").GetComponent<TextMeshProUGUI>();
                var answer = row.Find("Answer");
                var answerCaption = row.Find("ReferenceAnswer" + i);
                var questionCaption = row.Find("ReferenceQuestion" + i);
                questionCaption.gameObject.SetActive(false);
                answerCaption.gameObject.SetActive(false);
                questionCaption.GetComponent<ApprovedHudCaption>().enabled = false;
                answerCaption.GetComponent<ApprovedHudCaption>().enabled = false;
                question.GetComponent<CanvasGroup>().alpha = 1;
                answer.GetComponent<CanvasGroup>().alpha = 1;

                var questionRect = (RectTransform)question.transform;
                questionRect.sizeDelta = new Vector2(440 * S, 86 * S);
                questionRect.anchoredPosition = new Vector2(175 * S, -38 * S);
                var answerRect = (RectTransform)answer;
                answerRect.anchoredPosition = new Vector2(175 * S, -132 * S);
                answerRect.sizeDelta = new Vector2(494 * S, 110 * S);
                answer.gameObject.SetActive(false);

                var height = row.GetComponent<LayoutElement>();
                height.minHeight = height.preferredHeight = 190 * S;
                var plate = row.Find("Plate").GetComponent<Image>();
                plate.color = Color.white;
                var button = Button(row, plate);
                RemoveOldIconTile(row);
                MaskIcon(row, FindIcon(row));
                var accent = Decoration(row, "ActiveAccent", new Color32(0, 139, 219, 255));
                Accent(accent);
                var divider = Decoration(row, "ActiveDivider", new Color32(71, 171, 230, 190));
                Divider(divider, 126);
                var indicator = Indicator(row, sample, 22, 38);
                Bind(row, button, plate, indicator, null, height, 190 * S, 260 * S,
                    new[] { answer.gameObject }, new[] { accent.gameObject, divider.gameObject });

                if (i < 4)
                {
                    var separator = content.Find("Separator" + i);
                    var gap = separator.GetComponent<LayoutElement>();
                    gap.minHeight = gap.preferredHeight = 22 * S;
                    separator.Find("Line").gameObject.SetActive(false);
                }
            }

            content.Find("BeforeDetails").gameObject.SetActive(false);
            content.Find("DetailsTitle").gameObject.SetActive(false);

            for (int i = 1; i <= 7; i++)
            {
                var section = content.Find("Detail-FAQPanel_Question" + i);
                var description = section.GetComponentInChildren<FAQDesc>(true);
                var plate = section.Find("Plate").GetComponent<Image>();
                plate.color = Color.white;
                var button = Button(section, plate);
                var icon = FindIcon(section);
                if (icon == null)
                {
                    icon = UnityEngine.Object.Instantiate(sampleIcon, section).transform;
                    icon.name = "QuestionIcon";
                }
                var iconLayout = Ensure<LayoutElement>(icon);
                iconLayout.ignoreLayout = true;
                var iconRect = (RectTransform)icon;
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0, 1);
                iconRect.anchoredPosition = new Vector2(26 * S, -38 * S);
                iconRect.sizeDelta = new Vector2(119 * S, 124 * S);
                iconRect.localScale = Vector3.one;
                MaskIcon(section, icon);
                var iconMaskRect = (RectTransform)section.Find("QuestionIconMask");
                iconMaskRect.anchorMin = iconMaskRect.anchorMax = iconMaskRect.pivot = new Vector2(0, 1);
                iconMaskRect.anchoredPosition = new Vector2(26 * S, -38 * S);
                iconMaskRect.sizeDelta = new Vector2(119 * S, 124 * S);

                var header = Text(section, "Question", sample);
                header.alignment = TextAlignmentOptions.MidlineLeft;
                header.fontSize = 43 * S;
                header.fontSizeMax = 43 * S;
                header.fontSizeMin = 32 * S;
                header.enableAutoSizing = true;
                header.margin = new Vector4(140 * S, 0, 52 * S, 0);
                var headerHeight = Ensure<LayoutElement>(header.transform);
                headerHeight.minHeight = headerHeight.preferredHeight = 112 * S;
                header.transform.SetSiblingIndex(1);
                iconMaskRect.SetSiblingIndex(2);
                section.GetComponent<VerticalLayoutGroup>().spacing = 14 * S;

                RemoveOldIconTile(section);
                var accent = Decoration(section, "ActiveAccent", new Color32(0, 139, 219, 255));
                Accent(accent);
                var divider = Decoration(section, "ActiveDivider", new Color32(71, 171, 230, 190));
                Divider(divider, 157);
                var indicator = Indicator(section, sample, 22, 51);
                Bind(section, button, plate, indicator, header, null, 0, 0,
                    new[] { description.gameObject }, new[] { accent.gameObject, divider.gameObject });
                description.tMP_Text.margin = new Vector4(140 * S, 0, 14 * S, 0);
                description.tMP_Text.gameObject.SetActive(false);

                var after = content.Find("After-FAQPanel_Question" + i).GetComponent<LayoutElement>();
                after.minHeight = after.preferredHeight = 18 * S;
            }

            // Keep duplicate source text in the prefab, but show each topic once.
            // The daily-limit question is more specific than the general frequency question.
            foreach (int duplicate in new[] { 1, 3, 4 })
            {
                content.Find("Detail-FAQPanel_Question" + duplicate).gameObject.SetActive(false);
                content.Find("After-FAQPanel_Question" + duplicate).gameObject.SetActive(false);
            }

            if (root.GetComponentsInChildren<FAQAccordionItem>(true).Length != 11)
                throw new InvalidOperationException("Expected eleven FAQ accordions.");
            int visible = 0;
            foreach (var item in root.GetComponentsInChildren<FAQAccordionItem>(true))
                if (item.gameObject.activeInHierarchy) visible++;
            if (visible != 8) throw new InvalidOperationException("Expected eight distinct visible FAQ questions.");
        }

        static Image Decoration(Transform row, string name, Color color)
        {
            var child = row.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform)).transform;
                child.SetParent(row, false);
                child.gameObject.layer = 5;
            }
            var image = Ensure<Image>(child);
            image.color = color;
            image.raycastTarget = false;
            Ensure<LayoutElement>(child).ignoreLayout = true;
            child.SetSiblingIndex(1);
            child.gameObject.SetActive(false);
            return image;
        }

        static Transform FindIcon(Transform row)
        {
            return row.Find("QuestionIcon") ?? row.Find("QuestionIconMask/QuestionIcon");
        }

        static void RemoveOldIconTile(Transform row)
        {
            var old = row.Find("ActiveIconTile");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        static void MaskIcon(Transform row, Transform icon)
        {
            var sourceRect = (RectTransform)icon;
            var mask = row.Find("QuestionIconMask");
            if (mask == null)
            {
                mask = new GameObject("QuestionIconMask", typeof(RectTransform)).transform;
                mask.SetParent(row, false);
                mask.gameObject.layer = 5;
                var maskRect = (RectTransform)mask;
                maskRect.anchorMin = sourceRect.anchorMin;
                maskRect.anchorMax = sourceRect.anchorMax;
                maskRect.pivot = sourceRect.pivot;
                maskRect.anchoredPosition = sourceRect.anchoredPosition;
                maskRect.sizeDelta = sourceRect.sizeDelta;
                maskRect.localScale = sourceRect.localScale;
                mask.SetSiblingIndex(icon.GetSiblingIndex());
            }
            var image = Ensure<Image>(mask);
            image.sprite = CircleMaskSprite();
            image.raycastTarget = false;
            Ensure<Mask>(mask).showMaskGraphic = false;
            Ensure<LayoutElement>(mask).ignoreLayout = true;
            icon.SetParent(mask, false);
            sourceRect.anchorMin = sourceRect.anchorMax = sourceRect.pivot = new Vector2(.5f, .5f);
            sourceRect.anchoredPosition = Vector2.zero;
            sourceRect.sizeDelta = ((RectTransform)mask).sizeDelta;
            sourceRect.localScale = Vector3.one;
            var iconImage = icon.GetComponent<Image>();
            if (iconImage != null) iconImage.raycastTarget = false;
        }

        static Sprite CircleMaskSprite()
        {
            if (!File.Exists(CircleMaskPath))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size - .5f;
                    float v = (y + .5f) / size - .5f;
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        u * u + v * v <= .475f * .475f ? (byte)255 : (byte)0);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(CircleMaskPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(CircleMaskPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(CircleMaskPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleMaskPath);
            if (sprite == null) throw new InvalidOperationException("FAQ icon mask sprite is unavailable.");
            return sprite;
        }

        static void Accent(Image image)
        {
            var rect = (RectTransform)image.transform;
            rect.anchorMin = new Vector2(0, .08f);
            rect.anchorMax = new Vector2(0, .92f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(16 * S, 0);
            rect.sizeDelta = new Vector2(9 * S, 0);
        }

        static void Divider(Image image, float top)
        {
            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(175 * S, -top * S);
            rect.sizeDelta = new Vector2(474 * S, 3 * S);
        }

        static Button Button(Transform row, Image plate)
        {
            var button = Ensure<Button>(row);
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            plate.raycastTarget = true;
            return button;
        }

        static TextMeshProUGUI Indicator(Transform row, TextMeshProUGUI sample, float right, float top)
        {
            var indicator = Text(row, "ExpandIndicator", sample);
            var rect = (RectTransform)indicator.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-right * S, -top * S);
            rect.sizeDelta = new Vector2(48 * S, 68 * S);
            indicator.fontSize = 58 * S;
            indicator.enableAutoSizing = false;
            indicator.alignment = TextAlignmentOptions.Center;
            indicator.margin = Vector4.zero;
            indicator.color = new Color32(0, 110, 214, 255);
            indicator.text = "+";
            var layout = Ensure<LayoutElement>(indicator.transform);
            layout.ignoreLayout = true;
            indicator.transform.SetAsLastSibling();
            return indicator;
        }

        static TextMeshProUGUI Text(Transform parent, string name, TextMeshProUGUI sample)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform)).transform;
                child.SetParent(parent, false);
                child.gameObject.layer = 5;
            }
            var text = Ensure<TextMeshProUGUI>(child);
            text.font = sample.font;
            text.fontSharedMaterial = sample.fontSharedMaterial;
            text.fontStyle = FontStyles.Normal;
            text.color = new Color(.03f, .02f, .32f);
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

        static void Bind(Transform row, Button button, Image background, TMP_Text indicator, TMP_Text header,
            LayoutElement height, float collapsed, float expanded, GameObject[] answers, GameObject[] decorations)
        {
            var data = new SerializedObject(Ensure<FAQAccordionItem>(row));
            data.FindProperty("button").objectReferenceValue = button;
            data.FindProperty("background").objectReferenceValue = background;
            data.FindProperty("indicator").objectReferenceValue = indicator;
            data.FindProperty("detailedQuestion").objectReferenceValue = header;
            data.FindProperty("height").objectReferenceValue = height;
            data.FindProperty("collapsedHeight").floatValue = collapsed;
            data.FindProperty("expandedHeight").floatValue = expanded;
            var array = data.FindProperty("answerObjects");
            array.arraySize = answers.Length;
            for (int i = 0; i < answers.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = answers[i];
            array = data.FindProperty("expandedDecorations");
            array.arraySize = decorations.Length;
            for (int i = 0; i < decorations.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = decorations[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Ensure<T>(Component component) where T : Component
        {
            var found = component.GetComponent<T>();
            return found != null ? found : component.gameObject.AddComponent<T>();
        }
    }
}
