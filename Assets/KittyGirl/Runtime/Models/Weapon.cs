using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace ParallaxVisions.Models
{
    [System.Serializable]
    public class Weapon: System.Object
    {

        public float _Delay { get; set; }
        public bool needsAmmo { get; set; }
        public int _RemainingBullets { get; set; }
        public int _PowerBullet { get; set; }
        public GameObject _PrefabBullet { get; set; }

        public Weapon(float d,  int p, GameObject b, bool n = false, int r = 0)
        {
            this._Delay = d;
            this.needsAmmo = n;
            this._RemainingBullets = r;
            this._PowerBullet = p;
            this._PrefabBullet = b;

        }

    }
}