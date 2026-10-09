using TMPro;
using UnityEngine;

namespace BubblePics
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class CoralLocalizedLabel : MonoBehaviour
    {
        [SerializeField] string key;
        TMP_Text label;
        void OnEnable()
        {
            label=GetComponent<TMP_Text>();
            Localization.LocaleChanged+=Refresh;
            Refresh();
        }
        void OnDisable(){Localization.LocaleChanged-=Refresh;}
        public void SetKey(string value)
        {
            key=value;
            if(label==null)label=GetComponent<TMP_Text>();
            Refresh();
        }
        void Refresh(){label.text=Localization.Tr(key);}
    }
}
