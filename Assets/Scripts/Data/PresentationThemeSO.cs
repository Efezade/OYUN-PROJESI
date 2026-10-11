using UnityEngine;
namespace TacticalRPG.Data
{
    [CreateAssetMenu(menuName="TacticalRPG/Presentation Theme")]
    public sealed class PresentationThemeSO : ScriptableObject
    {
        [SerializeField] private Color _background=new(.035f,.052f,.063f,.97f);
        public Color background=>_background;
        [SerializeField] private Color _panel=new(.075f,.105f,.119f,.98f);
        public Color panel=>_panel;
        [SerializeField] private Color _raised=new(.12f,.17f,.18f,1);
        public Color raised=>_raised;
        [SerializeField] private Color _text=new(.91f,.91f,.84f,1);
        public Color text=>_text;
        [SerializeField] private Color _muted=new(.57f,.66f,.66f,1);
        public Color muted=>_muted;
        [SerializeField] private Color _accent=new(.71f,.57f,.32f,1);
        public Color accent=>_accent;
        [SerializeField] private Color _teal=new(.26f,.53f,.49f,1);
        public Color teal=>_teal;
        [SerializeField] private Sprite _panelSprite;
        public Sprite panelSprite=>_panelSprite;
        [SerializeField,Range(12,24)] private int _hudFontSize=15;
        public int hudFontSize=>_hudFontSize;
    }
}
