
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Silvainius.Worlds.Home
{
    public class ObjectToggle : UdonSharpBehaviour
    {
        public GameObject obj;
        public bool startActive;

        public void Start()
        {
            obj.SetActive(startActive);
        }

        public override void Interact()
        {
            obj.SetActive(!obj.activeSelf);
        }
    }
}
