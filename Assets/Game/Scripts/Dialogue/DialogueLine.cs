using Operator.Missions;
using UnityEngine;

namespace Operator.Dialogue
{
    [System.Serializable]
    public class DialogueLine
    {
        [SerializeField] SpeakerProfile speaker;
        [TextArea(2, 5)]
        [SerializeField] string text;

        public SpeakerProfile Speaker => speaker;
        public string Text => text;
    }
}
