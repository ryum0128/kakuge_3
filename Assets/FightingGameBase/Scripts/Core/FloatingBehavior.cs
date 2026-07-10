using UnityEngine;

namespace FightingGameBase
{
    public class FloatingBehavior : MonoBehaviour
    {
        public float amplitude = 0.1f;
        public float frequency = 2f;
        
        private Vector3 startPos;
        
        void Start()
        {
            startPos = transform.localPosition;
        }
        
        void Update()
        {
            transform.localPosition = startPos + new Vector3(0f, Mathf.Sin(Time.time * frequency) * amplitude, 0f);
        }
    }
}
