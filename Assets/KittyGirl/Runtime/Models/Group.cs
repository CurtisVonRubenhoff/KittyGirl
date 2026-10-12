using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ParallaxVisions;

namespace ParallaxVisions.Models
{
    [System.Serializable]
    public class Group: System.Object
    {
        [SerializeField]
        public Row Row1;
        [SerializeField]
        public Row Row2;
        [SerializeField]
        public Row Row3;
        [SerializeField]
        public int Distance;
    }
}
