using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Social;
using Il2CppRUMBLE.Social.Phone;
using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using RumbleModUI;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

[assembly: MelonInfo(typeof(AutomaticInviter.Core), "AutomaticInviter", "1.2.1", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace AutomaticInviter
{
    public class Core : MelonMod
    {
        private string[] usernameList;
        private string usernamesPath = "UserData/AutomaticInviter/usernames.txt";
        private string[] IDList;
        private string IDsPath = "UserData/AutomaticInviter/IDs.txt";
        private string[] CodesList;
        private string CodesListPath = "UserData/AutomaticInviter/FriendCodes.txt";
        internal static MelonLogger.Instance loggerInstance;
        private bool autoInvite = false;
        private AssetBundle assetBundle = null;
        private Mod mod = new Mod();
        public static string invokedFind = null;
        private static object inviteListByCodeCoroutine = null;
        internal static object waitingCoroutine = null;

        private static Dictionary<string, UserData> codeCache = new();

        public override void OnInitializeMelon()
        {
            Actions.onMapInitialized += OnMapInitialised;
            InitFiles();
            loggerInstance = LoggerInstance;
            assetBundle = AssetBundles.LoadAssetBundleFromStream(this, "AutomaticInviter.Resources.autoinvite");

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
            // Used for debugging so I don't have to go in headset

            /*if (Input.GetKeyDown(KeyCode.O))
            {
                MelonLogger.Msg("Inviting");
                GameObject optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                OptionsPage options = optionsObject.GetComponent<OptionsPage>();

                if (usernameList.Length >= 1)
                    InviteListByUsername(usernameList, options);
                if (IDList.Length >= 1)
                    InviteListByMasterID(IDList, options);
                if (CodesList.Length >= 1 && inviteListByCodeCoroutine == null)
                    inviteListByCodeCoroutine = MelonCoroutines.Start(InviteListByFriendCode(CodesList, options));
                MelonLogger.Msg("okay done");
            }*/
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

            if (!File.Exists(CodesListPath))
            {
                File.WriteAllText(CodesListPath, string.Empty);
            }

            UpdateLists();
        }

        private void UpdateLists()
        {
            usernameList = [];
            IDList = [];
            CodesList = [];

            usernameList = File.Exists(usernamesPath) ? File.ReadAllLines(usernamesPath) : Array.Empty<string>();
            IDList = File.Exists(IDsPath) ? File.ReadAllLines(IDsPath) : Array.Empty<string>();
            CodesList = File.Exists(CodesListPath) ? File.ReadAllLines(CodesListPath) : Array.Empty<string>();
        }

        public static void InvitePerson(string masterID, string titleID, string username, OptionsPage optionsPage)
        {
            if (optionsPage == null)
            {
                loggerInstance.Error("Options page is null!");
                return;
            }
            UserData friendData = new UserData(masterID, titleID, username);
            optionsPage.Initialize(friendData);
            optionsPage.inviteToParkButton.onPressed.Invoke();
        }

        public static void InvitePerson(UserData userData, OptionsPage optionsPage)
        {
            if (optionsPage == null)
            {
                loggerInstance.Error("Options page is null!");
                return;
            }
            optionsPage.Initialize(userData);
            optionsPage.inviteToParkButton.onPressed.Invoke();
        }

        public static void InvitePersonByFriendCode(string code, FindUser findUser)
        {
            foreach (char character in code)
            {
                findUser.OnNumberPressed((int)Char.GetNumericValue(character));
            }
            invokedFind = code;
        }

        private void OnStep(int step)
        {
            autoInvite = step == 1;
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (inviteListByCodeCoroutine != null)
            {
                MelonCoroutines.Stop(inviteListByCodeCoroutine);
                inviteListByCodeCoroutine = null;
            }
            if (waitingCoroutine != null)
            {
                MelonCoroutines.Stop(waitingCoroutine);
                waitingCoroutine = null;
            }

            codeCache = new();
        }

        private void OnMapInitialised(string sceneName)
        {
            if (sceneName == "Gym")
            {
                if (!(bool)mod.Settings[1].SavedValue)
                    autoInvite = false;

                GameObject HostPanel = GameObjects.Gym.INTERACTABLES.Parkboard.RotatingScreen.HostPanel.GetGameObject();
                GameObject DoorPilicy = HostPanel.transform.GetChild(1).gameObject;
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

                GameObject imageLeft = JoinInviteSetting.transform.GetChild(1).GetChild(1).gameObject;
                GameObject imageRight = JoinInviteSetting.transform.GetChild(1).GetChild(4).gameObject;

                imageLeft.transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
                imageRight.transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);

                Texture2D cross = assetBundle.LoadAsset<Texture2D>("cross");
                Texture2D checkmark = assetBundle.LoadAsset<Texture2D>("checkmark");

                imageLeft.GetComponent<MeshRenderer>().material.SetTexture("_Texture", cross);
                imageRight.GetComponent<MeshRenderer>().material.SetTexture("_Texture", checkmark);

                HostPanel.transform.position += new Vector3(0, 0.1f, 0);
                HostPanel.transform.GetChild(0).localPosition -= new Vector3(0, 0.1f, 0); // Put that back right now!
                DoorPilicy.transform.GetChild(0).GetChild(0).localPosition += new Vector3(0.02f, 0, 0);
                DoorPilicy.transform.GetChild(0).GetChild(0).localScale -= new Vector3(0, 0.01f, 0);

                for (int i = 1; i < HostPanel.transform.GetChild(2).GetChild(0).childCount; i++)
                {
                    Transform thisChild = HostPanel.transform.GetChild(2).GetChild(0).GetChild(i);
                    thisChild.localScale = new Vector3(thisChild.localScale.x, 0.035f, thisChild.localScale.z);
                }
            }

            if (sceneName != "Park" || !Calls.Players.IsHost()) return;

            UpdateLists();

            GameObject optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
            if (optionsObject == null)
            {
                loggerInstance.Error("Cannot find settings page object!");
                return;
            }

            OptionsPage options = optionsObject.GetComponent<OptionsPage>();
            if (options == null)
            {
                loggerInstance.Error("Cannot find options page!");
                return;
            }

            if (autoInvite)
            {
                loggerInstance.Msg("Inviting people...");
                if (usernameList.Length >= 1)
                    InviteListByUsername(usernameList, options);
                if (IDList.Length >= 1)
                    InviteListByMasterID(IDList, options);
                if (CodesList.Length >= 1 && inviteListByCodeCoroutine == null)
                    inviteListByCodeCoroutine = MelonCoroutines.Start(InviteListByFriendCode(CodesList, options));
                loggerInstance.Msg("People invited!");
            }

            GameObject shiftstoneButton = GameObjects.Park.INTERACTABLES.Shiftstones.ShiftstoneQuickswapper.FloatingButton.GetGameObject();
            GameObject inviteButtonObject = GameObject.Instantiate(shiftstoneButton);
            inviteButtonObject.transform.position = new Vector3(-29.2742f, -1.6354f, -7.015f);
            inviteButtonObject.transform.rotation = Quaternion.Euler(-0, 90, 0);
            inviteButtonObject.transform.GetChild(1).GetComponent<TextMeshPro>().text = "AutoInvite";
            GameObject.Destroy(inviteButtonObject.transform.GetChild(2).gameObject);
            GameObject inviteListButton = Create.NewButton();
            inviteListButton.name = "InviteListButton";
            inviteListButton.transform.GetChild(0).gameObject.GetComponent<InteractionButton>().onPressed.AddListener(new System.Action(() =>
            {
                loggerInstance.Msg("Inviting people...");
                if (usernameList.Length >= 1)
                    InviteListByUsername(usernameList, options);
                if (IDList.Length >= 1)
                    InviteListByMasterID(IDList, options);
                if (CodesList.Length >= 1 && inviteListByCodeCoroutine == null)
                    inviteListByCodeCoroutine = MelonCoroutines.Start(InviteListByFriendCode(CodesList, options));
                loggerInstance.Msg("People invited!");
            }));
            inviteListButton.transform.SetParent(inviteButtonObject.transform);
            inviteListButton.transform.localPosition = new Vector3(0.0326f, 0.0054f, 0.0458f);
            inviteListButton.transform.rotation = Quaternion.Euler(0, 180, 90);
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
                GameObject optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObject == null)
                {
                    loggerInstance.Error("Cannot find settings page object!");
                    return;
                }

                optionsPage = optionsObject.GetComponent<OptionsPage>();
                if (optionsPage == null)
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
                        if (Calls.Players.GetAllPlayers().ToArray().Where(player => player.Data.GeneralData.PlayFabMasterId == friend.PlayFabMasterId).Count() != 0)
                        {
                            loggerInstance.Msg($"Inviting {friend.PublicName}");
                            InvitePerson(friend.PlayFabMasterId, friend.PlayFabTitleId, friend.PublicName, optionsPage);
                        }
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
                GameObject optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObject == null)
                {
                    loggerInstance.Error("Cannot find settings page object!");
                    return;
                }

                optionsPage = optionsObject.GetComponent<OptionsPage>();
                if (optionsPage == null)
                {
                    loggerInstance.Error("Cannot find options page!");
                    return;
                }
            }

            Il2CppSystem.Collections.Generic.List<GetFriendsResult> friends = FriendHandler.ConfirmedFriends;
            foreach (string masterID in IDs)
            {
                if (Calls.Players.GetAllPlayers().ToArray().Where(player => player.Data.GeneralData.PlayFabMasterId == masterID).Count() != 0)
                {
                    return;
                }
                System.Collections.Generic.IEnumerable<GetFriendsResult> friendsWithID = friends.ToArray().Where(friend => friend.PlayFabMasterId == masterID);

                if (friendsWithID.Count() == 0)
                {
                    loggerInstance.Msg($"No friends with ID \"{masterID}\"");
                    UserData userData = UserDataManager.FetchUserData(masterID);

                    if (userData != null)
                    {
                        loggerInstance.Msg($"Inviting ID {userData.playFabMasterId} - {userData.publicName}");
                        InvitePerson(userData.playFabMasterId, userData.playFabTitleId, userData.publicName, optionsPage);
                    }
                    else
                    {
                        loggerInstance.Warning($"Could not find user - {masterID}");
                    }
                }
                else
                {
                    GetFriendsResult friend = friendsWithID.First();
                    loggerInstance.Msg($"Inviting ID {friend.PlayFabMasterId} - {friend.PublicName}");
                    InvitePerson(friend.PlayFabMasterId, friend.PlayFabTitleId, friend.PublicName, optionsPage);
                }
            }
        }

        public static IEnumerator InviteListByFriendCode(string[] codes, OptionsPage optionsPage = null)
        {
            if (codes.Length < 1)
            {
                loggerInstance.Error("Code list is empty!");
                inviteListByCodeCoroutine = null;
                yield break;
            }

            if (optionsPage == null)
            {
                GameObject optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObject == null)
                {
                    loggerInstance.Error("Cannot find settings page object!");
                    inviteListByCodeCoroutine = null;
                    yield break;
                }

                optionsPage = optionsObject.GetComponent<OptionsPage>();
                if (optionsPage == null)
                {
                    loggerInstance.Error("Cannot find options page!");
                    inviteListByCodeCoroutine = null;
                    yield break;
                }
            }

            GameObject friendScreen = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.PlayerFinderScreen.GetGameObject();
            FindUser findUser = friendScreen.GetComponent<FindUser>();

            foreach (string code in codes)
            {
                if (codeCache.ContainsKey(code))
                {
                    loggerInstance.Msg($"Inviting player with friend code {code}");
                    UserData userData = codeCache[code];
                    if (Calls.Players.GetAllPlayers().ToArray().Where(player => player.Data.GeneralData.PlayFabMasterId == userData.playFabMasterId).Count() == 0)
                    {
                        InvitePerson(userData, optionsPage);
                    }
                    inviteListByCodeCoroutine = null;
                    yield break;
                }
                loggerInstance.Msg($"Finding player with friend code {code}");
                InvitePersonByFriendCode(code, findUser);
                while (invokedFind != null)
                {
                    yield return null;
                }
                for (int i = 1; i <= 8; i++)
                {
                    findUser.OnBackspacePressed();
                }
            }
            inviteListByCodeCoroutine = null;
            yield break;
        }

        internal static IEnumerator WaitForUserData(PlayerTag playerTag, OptionsPage options, string code)
        {
            int count = 0;
            while (playerTag.UserData.publicName == "" && count < 5000) // For me it usually takes around 200 +- 50 but idk if it's affected by lag so 5000 frames should be a good timeout
            {
                count++;
                yield return null;
            }

            if (count == 5000)
            {
                loggerInstance.Warning("Search for user timed out");
                yield break;
            }

            if (Calls.Players.GetAllPlayers().ToArray().Where(player => player.Data.GeneralData.PlayFabMasterId == playerTag.UserData.playFabMasterId).Count() != 0)
            {
                yield break;
            }

            Core.codeCache.Add(code, playerTag.UserData);

            options.Initialize(playerTag.UserData);
            loggerInstance.Msg($"Inviting player with friend code {code}");
            InvitePerson(playerTag.UserData, options);
            yield break;
        }
    }

    [HarmonyPatch(typeof(FindUser), "OnDoneFindingUser")]
    class FindUserPatch
    {
        static void Postfix(ref FindUser __instance)
        {
            if (Core.invokedFind != null)
            {
                GameObject optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObject == null)
                {
                    Core.loggerInstance.Error("Cannot find settings page object!");
                    return;
                }

                OptionsPage options = optionsObject.GetComponent<OptionsPage>();
                if (options == null)
                {
                    Core.loggerInstance.Error("Cannot find options page!");
                    return;
                }

                if (__instance.searchStatusText.text != "No ID match found")
                {
                    Core.waitingCoroutine = MelonCoroutines.Start(Core.WaitForUserData(__instance.PlayerTag, options, Core.invokedFind));
                }
                else
                {
                    Core.loggerInstance.Warning($"No user found with friend code {Core.invokedFind}");
                }
                Core.invokedFind = null;
            }
        }
    }
}