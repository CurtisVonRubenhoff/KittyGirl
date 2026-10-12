using UnityEngine;

namespace ParallaxVisions.Models
{
    [CreateAssetMenu(menuName = "KGF/Enemies/Group")]
    public class GroupSO: ScriptableObject
    {
        public Row Row1;
        public Row Row2;
        public Row Row3;
        public int Distance;
    }
}