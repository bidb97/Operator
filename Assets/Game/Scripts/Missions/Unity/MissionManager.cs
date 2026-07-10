using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Missions;
using Operator.Missions.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Operator.Missions.Unity
{
    /// <summary>Миссии: автовыдача, брифинг, маркер цели, HUD сектора.</summary>
    public class MissionManager : MonoBehaviour
    {
        [SerializeField] MissionCatalog missionCatalog;
        [SerializeField] Transform drone;
        [SerializeField] GameObject briefingPanel;
        [SerializeField] TextMeshProUGUI speakerText;
        [SerializeField] TextMeshProUGUI messageText;
        [SerializeField] Image avatarImage;
        [SerializeField] NavigationHud navigationHud;

        bool _wasAtBase;
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

            if (avatarImage == null && briefingPanel != null)
            {
                var avatar = briefingPanel.transform.Find("Avatar");
                if (avatar != null)
                    avatarImage = avatar.GetComponent<Image>();
            }
        }

        void Start()
        {
            _wasAtBase = IsAtBase();
            _trackedMission = GameBootstrap.Instance?.Session?.ActiveMission;
            RefreshMissionPresentation(forceBriefing: true);
        }

        void Update()
        {
            var mission = GameBootstrap.Instance?.Session?.ActiveMission;
            if (!ReferenceEquals(mission, _trackedMission))
            {
                _trackedMission = mission;
                RefreshMissionPresentation(forceBriefing: false);
            }

            var atBase = IsAtBase();
            if (atBase != _wasAtBase)
            {
                _wasAtBase = atBase;
                RefreshMissionPresentation(forceBriefing: false);
            }
        }

        void RefreshMissionPresentation(bool forceBriefing)
        {
            var mission = GameBootstrap.Instance?.Session?.ActiveMission;

            if (mission == null)
            {
                SetBriefingVisible(false);
                navigationHud?.HideTarget();
                return;
            }

            navigationHud?.ShowTarget(mission.TargetCell);

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
    }
}
