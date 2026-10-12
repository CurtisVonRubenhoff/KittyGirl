using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ParallaxVisions;

    namespace ParallaxVisions.Models
    {
        [System.Serializable]
        public class Wave: System.Object
        {
            [SerializeField]
            public int WaveCount;

            [SerializeField]
            public Group Group1;
            [SerializeField]
            public Group Group2;
            [SerializeField]
            public Group Group3;

        }
    }