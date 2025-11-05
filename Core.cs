using Il2CppInterop.Runtime;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Social;
using Il2CppRUMBLE.Social.Phone;
using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI;
using RumbleModUI;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Il2CppRUMBLE.Managers.PlatformManager;

[assembly: MelonInfo(typeof(AutomaticInviter.Core), "AutomaticInviter", "1.1.1", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace AutomaticInviter
{
    public class Core : MelonMod
    {
        private string[] usernameList;
        private string usernamesPath = "UserData/AutomaticInviter/usernames.txt";
        private string[] IDList;
        private string IDsPath = "UserData/AutomaticInviter/IDs.txt";
        public static OptionsPage options = null;
        private static MelonLogger.Instance loggerInstance;
        bool autoInvite = false;
        AssetBundle assetBundle = null;
        Mod mod = new Mod();


        public override void OnInitializeMelon()
        {
            Calls.onMapInitialized += OnMapInitialised;
            InitFiles();
            loggerInstance = LoggerInstance;
            assetBundle = Calls.LoadAssetBundleFromStream(this, "AutomaticInviter.Resources.autoinvite");

            mod.ModName = Info.Name;
            mod.ModVersion = Info.Version;
            mod.SetFolder(mod.ModName);
            mod.AddDescription("Description", "", "Invite a list of people to your park automatically!", new Tags { IsSummary = true });

            mod.AddToList("Persistent inviting on open", false, 0, "If disabled, the \"Auto invite on open\" setting will always be off when you enter the gym (Does not currently save when you close your game)", new Tags());
            mod.GetFromFile();

            UI.instance.UI_Initialized += OnUIInit;

            loggerInstance.Msg("Initialised.");
        }

        public void OnUIInit()
        {
            UI.instance.AddMod(mod);
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                loggerInstance.Msg("Refreshing lists...");
                UpdateLists();
            }
        }

        private void InitFiles()
        {
            if (!Directory.Exists("UserData"))
            {
                Directory.CreateDirectory("UserData");
            }

            if (!Directory.Exists("UserData/AutomaticInviter"))
            {
                Directory.CreateDirectory("UserData/AutomaticInviter");
            }

            if (!File.Exists(usernamesPath))
            {
                File.WriteAllText(usernamesPath, string.Empty);
            }

            if (!File.Exists(IDsPath))
            {
                File.WriteAllText(IDsPath, string.Empty);
            }

            UpdateLists();
        }

        private void UpdateLists()
        {
            usernameList = [];
            IDList = [];

            usernameList = File.Exists(usernamesPath) ? File.ReadAllLines(usernamesPath) : Array.Empty<string>();
            IDList = File.Exists(IDsPath) ? File.ReadAllLines(IDsPath) : Array.Empty<string>();
        }

        public static void InvitePerson(Platform platform, string masterID, string titleID, string username, OptionsPage optionsPage)
        {
            if (optionsPage == null)
            {
                loggerInstance.Error("Options page is null!");
                return;
            }
            UserData friendData = new UserData(platform, masterID, titleID, username);
            optionsPage.Initialize(friendData);
            optionsPage.inviteToParkButton.onPressed.Invoke();
        }

        private void OnStep(int step)
        {
            autoInvite = step == 1;
        }

        private void OnMapInitialised()
        {
            options = null;

            if (Calls.Scene.GetSceneName() == "Gym")
            {
                if (!(bool)mod.Settings[1].SavedValue)
                    autoInvite = false;
                GameObject DoorPilicy = Calls.GameObjects.Gym.LOGIC.Heinhouserproducts.Parkboard.RotatingScreen.HostPanel.DoorPilicy.GetGameObject();
                GameObject JoinInviteSetting = GameObject.Instantiate(DoorPilicy);

                JoinInviteSetting.transform.SetParent(DoorPilicy.transform.parent);

                Vector3 pos = DoorPilicy.transform.position;
                Quaternion rot = DoorPilicy.transform.rotation;

                JoinInviteSetting.transform.position = pos - new Vector3(0, 0.729f, 0);
                JoinInviteSetting.transform.rotation = rot;
                JoinInviteSetting.transform.GetChild(1).GetComponentInChildren<TextMeshPro>().text = "Auto invite on open?";

                InteractionSlider slider = JoinInviteSetting.transform.GetChild(2).GetChild(8).GetComponent<InteractionSlider>();
                if ((bool)mod.Settings[1].SavedValue)
                    slider.SetStep(autoInvite ? 1 : 0);

                Il2CppSystem.Action<int> action = new System.Action<int>((int step) =>
                {
                    autoInvite = step == 1;
                });
                UnityAction<int> stepReachedAction = DelegateSupport.ConvertDelegate<UnityAction<int>>(OnStep);
                slider.onStepReached.AddListener(stepReachedAction);

                Image imageLeft = JoinInviteSetting.transform.GetChild(3).GetChild(0).GetComponent<Image>();
                Texture2D cross = assetBundle.LoadAsset<Texture2D>("cross");
                Texture2D checkmark = assetBundle.LoadAsset<Texture2D>("checkmark");
                imageLeft.sprite = Sprite.Create(
                    cross,
                    new Rect(0, 0, cross.width, cross.height),
                    new Vector2(0.5f, 0.5f)
                );
                Image imageRight = JoinInviteSetting.transform.GetChild(3).GetChild(1).GetComponent<Image>();
                imageRight.sprite = Sprite.Create(
                    checkmark,
                    new Rect(0, 0, checkmark.width, checkmark.height),
                    new Vector2(0.5f, 0.5f)
                );
            }

            if (Calls.Scene.GetSceneName() != "Park" || !Calls.Players.IsHost()) return;

            UpdateLists();

            GameObject optionsObject = Calls.GameObjects.Park.LOGIC.Heinhouwserproducts.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
            if (optionsObject == null)
            {
                loggerInstance.Error("Cannot find settings page object!");
                return;
            }

            options = optionsObject.GetComponent<OptionsPage>();
            if (options == null)
            {
                loggerInstance.Error("Cannot find options page!");
                return;
            }

            if (autoInvite)
            {
                if (usernameList.Length >= 1)
                    InviteListByUsername(usernameList, options);
                if (IDList.Length >= 1)
                    InviteListByMasterID(IDList, options);
            }

            GameObject shiftstoneButton = Calls.GameObjects.Park.LOGIC.ShiftstoneQuickswapper.FloatingButton.GetGameObject();
            GameObject inviteButtonObject = GameObject.Instantiate(shiftstoneButton);
            inviteButtonObject.transform.position = new Vector3(-28.6742f, -1.6354f, -10.215f);
            inviteButtonObject.transform.rotation = Quaternion.Euler(-0, 70, 0);
            inviteButtonObject.transform.GetChild(1).GetComponent<TextMeshPro>().text = "AutoInvite";
            GameObject.Destroy(inviteButtonObject.transform.GetChild(2).gameObject);
            GameObject inviteListButton = Calls.Create.NewButton();
            inviteListButton.name = "InviteListButton";
            inviteListButton.transform.GetChild(0).gameObject.GetComponent<InteractionButton>().onPressed.AddListener(new System.Action(() =>
            {
                loggerInstance.Msg("Inviting people...");
                if (usernameList.Length >= 1)
                    InviteListByUsername(usernameList, options);
                if (IDList.Length >= 1)
                    InviteListByMasterID(IDList, options);
                loggerInstance.Msg("People invited!");
            }));
            inviteListButton.transform.SetParent(inviteButtonObject.transform);
            inviteListButton.transform.localPosition = new Vector3(0.0326f, 0.0054f, 0.0458f);
            inviteListButton.transform.rotation = Quaternion.Euler(0, 157, 90);
        }

        public static void InviteListByUsername(string[] usernames, OptionsPage optionsPage = null)
        {
            if (usernames.Length < 1)
            {
                loggerInstance.Error("Username list is empty!");
                return;
            }

            if (optionsPage == null)
            {
                GameObject optionsObject = Calls.GameObjects.Park.LOGIC.Heinhouwserproducts.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObject == null)
                {
                    loggerInstance.Error("Cannot find settings page object!");
                    return;
                }

                options = optionsObject.GetComponent<OptionsPage>();
                if (options == null)
                {
                    loggerInstance.Error("Cannot find options page!");
                    return;
                }
            }

            Il2CppSystem.Collections.Generic.List<GetFriendsResult> friends = FriendHandler.ConfirmedFriends;
            foreach (string username in usernames)
            {
                System.Collections.Generic.IEnumerable<GetFriendsResult> friendsWithUsername = friends.ToArray().Where(friend => friend.PublicName == username || Regex.Replace(friend.PublicName, @"<#[0-9A-Fa-f]{3,6}>", "") == username);
                if (friendsWithUsername.Count() > 1)
                {
                    loggerInstance.Warning($"Multiple friends found with username \"{username},\" please use their ID instead!");
                }
                else if (friendsWithUsername.Count() == 0)
                {
                    loggerInstance.Warning($"No friends with username \"{username}\"");
                }
                else
                {
                    foreach (GetFriendsResult friend in friendsWithUsername)
                    {
                        if (Calls.Players.GetAllPlayers().ToArray().Where(player => player.Data.GeneralData.PlayFabMasterId == friend.PlayFabMasterId).Count() == 0)
                            InvitePerson(friend.PlatformId, friend.PlayFabMasterId, friend.PlayFabTitleId, friend.PublicName, optionsPage);
                    }
                }
            }
        }

        public static void InviteListByMasterID(string[] IDs, OptionsPage optionsPage = null)
        {
            if (IDs.Length < 1)
            {
                loggerInstance.Error("ID list is empty!");
                return;
            }

            if (optionsPage == null)
            {
                GameObject optionsObject = Calls.GameObjects.Park.LOGIC.Heinhouwserproducts.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObject == null)
                {
                    loggerInstance.Error("Cannot find settings page object!");
                    return;
                }

                options = optionsObject.GetComponent<OptionsPage>();
                if (options == null)
                {
                    loggerInstance.Error("Cannot find options page!");
                    return;
                }
            }

            Il2CppSystem.Collections.Generic.List<GetFriendsResult> friends = FriendHandler.ConfirmedFriends;
            foreach (string masterID in IDs)
            {
                System.Collections.Generic.IEnumerable<GetFriendsResult> friendsWithID = friends.ToArray().Where(friend => friend.PlayFabMasterId == masterID);

                if (friendsWithID.Count() == 0)
                {
                    loggerInstance.Warning($"No friends with ID \"{masterID}\"");
                }
                else
                {
                    GetFriendsResult friend = friendsWithID.First();
                    if (Calls.Players.GetAllPlayers().ToArray().Where(player => player.Data.GeneralData.PlayFabMasterId == friend.PlayFabMasterId).Count() == 0)
                        InvitePerson(friend.PlatformId, friend.PlayFabMasterId, friend.PlayFabTitleId, friend.PublicName, optionsPage);
                }
            }
        }
    }
}