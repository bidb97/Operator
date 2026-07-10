using UnityEngine;

namespace Operator.Missions
{
    [CreateAssetMenu(fileName = "SpeakerProfile", menuName = "Operator/Speaker Profile")]
    public class SpeakerProfile : ScriptableObject
    {
        [SerializeField] string title = "VIYA";
        [SerializeField] Sprite avatar;

        public string Title => title;
        public Sprite Avatar => avatar;
    }
}
