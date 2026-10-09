#if BIZZA_REAL_WITHDRAW
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    // All positions, artwork and typography are authored in the shared form prefab.
    // Runtime only selects the configured presentation for the selected payment method.
    public sealed class WithdrawalFormPresentation : MonoBehaviour
    {
        [Serializable] public sealed class RectState
        {
            public RectTransform target;public string authoredPath;public Vector2 anchorMin,anchorMax,pivot,position,size;public Vector3 scale;
            public void Apply(){if(target==null)throw new InvalidOperationException("Missing authored form rectangle: "+authoredPath);target.anchorMin=anchorMin;target.anchorMax=anchorMax;target.pivot=pivot;target.anchoredPosition=position;target.sizeDelta=size;target.localScale=scale;}
        }
        [Serializable] public sealed class ImageState
        {
            public Image target;public CoralResourceSprite resource;public string path,spriteName;public Sprite sprite;public Material material;
            public Color color;public Image.Type type;public bool enabled,raycast,preserveAspect;public float pixelsPerUnit;
            public void Apply(){if(resource!=null&&!string.IsNullOrEmpty(path))resource.SetSource(path,spriteName);else target.sprite=sprite;target.material=material;target.color=color;target.type=type;target.enabled=enabled;target.raycastTarget=raycast;target.preserveAspect=preserveAspect;target.pixelsPerUnitMultiplier=pixelsPerUnit;}
        }
        [Serializable] public sealed class TextState
        {
            public TMP_Text target;public TMP_FontAsset font;public Material material;public float size,min,max;public bool autoSize,wrap,gradient;public Color color;public VertexGradient gradientColors;public TextAlignmentOptions alignment;public Vector4 margin;
            public void Apply(){target.font=font;target.fontSharedMaterial=material;target.fontSize=size;target.fontSizeMin=min;target.fontSizeMax=max;target.enableAutoSizing=autoSize;target.enableWordWrapping=wrap;target.color=color;target.enableVertexGradient=gradient;target.colorGradient=gradientColors;target.alignment=alignment;target.margin=margin;target.UpdateMeshPadding();}
        }
        [Serializable] public sealed class EnabledState {public Behaviour target;public bool enabled;public void Apply(){target.enabled=enabled;}}
        [Serializable] public sealed class ActiveState {public GameObject target;public bool active;public void Apply(){target.SetActive(active);}}
        [Serializable] public sealed class LabelState {public CoralLocalizedLabel target;public string key;public void Apply(){if(target.enabled)target.SetKey(key);}}
        [Serializable] public sealed class GroupState {public CanvasGroup target;public float alpha;public void Apply(){target.alpha=alpha;}}
        [Serializable] public sealed class PromptState {public AdvancedInputFieldPlugin.AdvancedInputField target;public string key,original;public void Apply(){target.PlaceHolderText=string.IsNullOrEmpty(key)?original:Localization.Tr(key);}}
        [Serializable] public sealed class Preset
        {
            public string name;public string[] channels;public bool fixedLayout;public WithdrawWay methodPrefab;public RectState[] rects;public ImageState[] images;public TextState[] texts;public EnabledState[] behaviours;public ActiveState[] objects;public LabelState[] labels;public GroupState[] groups;public PromptState[] prompts;
            public void Apply()
            {
                foreach(var state in behaviours)state.Apply();foreach(var state in rects)state.Apply();foreach(var state in images)state.Apply();foreach(var state in texts)state.Apply();foreach(var state in labels)state.Apply();foreach(var state in groups)state.Apply();foreach(var state in objects)state.Apply();if(prompts!=null)foreach(var state in prompts)state.Apply();
            }
        }
        public Preset[] presets;
        Preset current;
        UIWithdrawalPanel controller;
        void Awake(){controller=GetComponent<UIWithdrawalPanel>();}
        void OnEnable(){Localization.LocaleChanged+=RefreshPrompts;}
        void OnDisable(){Localization.LocaleChanged-=RefreshPrompts;}
        void RefreshPrompts(){if(current?.prompts!=null)foreach(var state in current.prompts)state.Apply();if(current!=null&&controller!=null)controller.RefreshLocalizedInputHints();}
        public bool FixedLayout=>current!=null&&current.fixedLayout;
        public WithdrawWay MethodPrefab=>current?.methodPrefab;
        public bool Select(string channel)
        {
            if(presets==null||presets.Length==0)return false;
            Preset match=presets[0];
            for(int i=1;i<presets.Length;i++)foreach(string candidate in presets[i].channels)if(candidate==channel){match=presets[i];break;}
            if(current==match)return false;match.Apply();current=match;return true;
        }
    }
}
#endif
