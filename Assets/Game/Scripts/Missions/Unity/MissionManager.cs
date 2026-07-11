using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Missions;
using Operator.Missions.Core;
using UnityEngine;

namespace Operator.Missions.Unity
{
    /// <summary>Миссии: автовыдача, маркер цели, HUD сектора.</summary>
    public class MissionManager : MonoBehaviour
    {
        [SerializeField] MissionCatalog missionCatalog;
        [SerializeField] Transform drone;
        [SerializeField] NavigationHud navigationHud;

        ActiveMission _trackedMission;

        void Awake()
        {
            if (drone == null)
            {
                var controller = FindFirstObjectByType<DroneController>();
                if (controller != null)
                {
                    drone = controller.transform;
                }
            }

            if (navigationHud == null)
            {
                navigationHud = GetComponent<NavigationHud>();
            }
        }

        void Start()
        {
            _trackedMission = GameBootstrap.Instance?.Session?.ActiveMission;
            RefreshTarget();
        }

        void Update()
        {
            var mission = GameBootstrap.Instance?.Session?.ActiveMission;
            if (!ReferenceEquals(mission, _trackedMission))
            {
                _trackedMission = mission;
                RefreshTarget();
            }
        }

        void RefreshTarget()
        {
            var mission = GameBootstrap.Instance?.Session?.ActiveMission;

            if (mission == null)
            {
                navigationHud?.HideTarget();
                return;
            }

            navigationHud?.ShowTarget(mission.TargetCell);
        }

        // Показ брифинга миссии на панели отключён — панель теперь DialoguePanel,
        // текст миссии переезжает в журнал. Код ниже оставлен для журнала.
        /*
        [SerializeField] GameObject briefingPanel;
        [SerializeField] TextMeshProUGUI speakerText;
        [SerializeField] TextMeshProUGUI messageText;
        [SerializeField] Image avatarImage;

        bool _wasAtBase;

        void RefreshMissionPresentation(bool forceBriefing)
        {
            var mission = GameBootstrap.Instance?.Session?.ActiveMission;

            if (mission == null)
            {
                SetBriefingVisible(false);
                return;
            }

            var showBriefing = forceBriefing || IsAtBase();
            if (showBriefing)
            {
                if (speakerText != null)
                {
                    speakerText.text = mission.SpeakerTitle ?? string.Empty;
                }

                if (messageText != null)
                {
                    messageText.text = mission.BriefingText ?? string.Empty;
                }

                if (avatarImage != null)
                {
                    var avatar = ResolveAvatar(mission);
                    if (avatar != null)
                    {
                        avatarImage.sprite = avatar;
                        avatarImage.preserveAspect = true;
                        avatarImage.enabled = true;
                    }
                }
            }

            SetBriefingVisible(showBriefing);
        }

        Sprite ResolveAvatar(ActiveMission mission)
        {
            if (mission == null)
                return null;

            if (mission.SpeakerAvatar != null)
                return mission.SpeakerAvatar;

            var template = missionCatalog?.FindByTemplateId(mission.TemplateId);
            return template?.SpeakerAvatar;
        }

        void SetBriefingVisible(bool visible)
        {
            if (briefingPanel != null)
            {
                briefingPanel.SetActive(visible);
            }
        }

        bool IsAtBase()
        {
            if (drone == null)
            {
                return true;
            }

            var cell = WorldGrid.WorldToCell(drone.position);
            return cell.x == GarageBounds.ShaftX && cell.y <= GarageBounds.SecondCell.y;
        }
        */
    }
}
