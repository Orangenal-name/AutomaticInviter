using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Social;
using Il2CppRUMBLE.Social.Phone;
using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

[assembly: MelonInfo(typeof(AutomaticInviter.Core), "AutomaticInviter", "2.0.2", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace AutomaticInviter
{
    public class Core : MelonMod
    {
        private static string[] usernameList;
        private static string usernamesPath = "UserData/AutomaticInviter/usernames.txt";
        private static string[] IDList;
        private static string IDsPath = "UserData/AutomaticInviter/IDs.txt";
        private static string[] CodesList;
        private static string CodesListPath = "UserData/AutomaticInviter/FriendCodes.txt";
        internal static MelonLogger.Instance loggerInstance;
        private bool autoInvite = false;
        private AssetBundle assetBundle = null;
        public static string invokedFind = null;
        private static object inviteListByCodeCoroutine = null;
        internal static object waitingCoroutine = null;
        internal static string currentScene = "Loader";

        private static Dictionary<string, UserData> codeCache = new();

        public override void OnInitializeMelon()
        {
            Actions.onMapInitialized += OnMapInitialised;
            InitFiles();
            loggerInstance = LoggerInstance;
            assetBundle = AssetBundles.LoadAssetBundleFromStream(this, "AutomaticInviter.autoinvite");

            loggerInstance.Msg("Initialised.");
        }


        // Used for debugging so I don't have to go in headset
        #if DEBUG
        public static void DebugFunction()
        {
            UpdateLists();
            MelonLogger.Msg("Inviting");
            OptionsPage options;

            if (currentScene == "Gym")
            {
                GameObject gymOptionsObject = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                options = gymOptionsObject.GetComponent<OptionsPage>();
            }
            else if (currentScene == "Park")
            {
                GameObject parkOptionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                options = parkOptionsObject.GetComponent<OptionsPage>();
            }
            else
            {
                return;
            }

            if (usernameList.Length >= 1)
                InviteListByUsername(usernameList, options);
            if (IDList.Length >= 1)
                InviteListByMasterID(IDList, options);
            if (CodesList.Length >= 1 && inviteListByCodeCoroutine == null)
                inviteListByCodeCoroutine = MelonCoroutines.Start(InviteListByFriendCode(CodesList, options));
            MelonLogger.Msg("okay done");
        }
        #endif

        private void InitFiles() // If files don't exist, make them. Then update the lists if needed
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

        private static void UpdateLists() // Self-explanatory
        {
            usernameList = [];
            IDList = [];
            CodesList = [];

            usernameList = File.Exists(usernamesPath) ? File.ReadAllLines(usernamesPath) : Array.Empty<string>();
            IDList = File.Exists(IDsPath) ? File.ReadAllLines(IDsPath) : Array.Empty<string>();
            CodesList = File.Exists(CodesListPath) ? File.ReadAllLines(CodesListPath) : Array.Empty<string>();
        }

        public static void InvitePerson(string masterID, string titleID, string username, OptionsPage optionsPage) // For inviting friends by PlayFab ID
        {
            if (optionsPage == null)
            {
                loggerInstance.Error("Options page is null!");
                return;
            }
            UserData friendData = new UserData(masterID, titleID, username);
            optionsPage.Initialize(friendData);
            optionsPage.inviteButton.onPressed.Invoke();
        }

        public static void InvitePerson(UserData userData, OptionsPage optionsPage) // For inviting non-friends once UserData is fetched
        {
            if (optionsPage == null)
            {
                loggerInstance.Error("Options page is null!");
                return;
            }
            optionsPage.Initialize(userData);
            optionsPage.inviteButton.onPressed.Invoke();
        }

        public static void InvitePersonByFriendCode(string code, FindUser findUser)
        {
            foreach (char character in code)
            {
                if (character == '-') continue; // Foolproof
                findUser.OnNumberPressed((int)Char.GetNumericValue(character));
            }
            invokedFind = code;
        }

        private void OnStep(int step) // Sets the variable for the slider on the park board (gym)
        {
            autoInvite = step == 1;
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName) // Clear things up
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
            currentScene = sceneName;
            if (sceneName == "Gym")
            {
                autoInvite = false;
                
                // Set up the park board slider
                GameObject HostPanel = GameObjects.Gym.INTERACTABLES.Parkboard.RotatingScreen.HostPanel.GetGameObject();
                GameObject DoorPilicy = HostPanel.transform.GetChild(1).gameObject;
                GameObject JoinInviteSetting = GameObject.Instantiate(DoorPilicy, DoorPilicy.transform.parent, true);
                JoinInviteSetting.name = "AutoInvite Pilicy";

                Vector3 pos = DoorPilicy.transform.position;
                Quaternion rot = DoorPilicy.transform.rotation;

                JoinInviteSetting.transform.position = pos - new Vector3(0, 0.729f, 0);
                JoinInviteSetting.transform.rotation = rot;
                JoinInviteSetting.transform.GetChild(1).GetComponentInChildren<TextMeshPro>().text = "Auto invite on open?";

                InteractionSlider slider = JoinInviteSetting.transform.GetChild(2).GetChild(8).GetComponent<InteractionSlider>();

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
                
                GameObject optionsObjectGym = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                if (optionsObjectGym == null)
                {
                    loggerInstance.Error("Cannot find settings page object!");
                    return;
                }

                OptionsPage optionsGym = optionsObjectGym.GetComponent<OptionsPage>();
                if (optionsGym == null)
                {
                    loggerInstance.Error("Cannot find options page!");
                    return;
                }
                
                // Set up invite button
                GameObject shiftstoneButtonGym = GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneQuickswapper.FloatingButton.GetGameObject();
                GameObject inviteButtonObjectGym = GameObject.Instantiate(shiftstoneButtonGym);
                inviteButtonObjectGym.name = "AutoInvite Button";
                inviteButtonObjectGym.transform.position = new Vector3(8.1477f, 1.321f, 14.2382f);;
                inviteButtonObjectGym.transform.rotation = Quaternion.Euler(0, 197.4544f, 0);;
                inviteButtonObjectGym.transform.GetChild(1).GetComponent<TextMeshPro>().text = "AutoInvite";
                GameObject.Destroy(inviteButtonObjectGym.transform.GetChild(2).gameObject);
                GameObject inviteListButtonGym = Create.NewButton();
                inviteListButtonGym.name = "InviteListButton";
                inviteListButtonGym.transform.GetChild(0).gameObject.GetComponent<InteractionButton>().onPressed.AddListener(new System.Action(() =>
                {
                    UpdateLists();
                    loggerInstance.Msg("Inviting people...");
                    if (usernameList.Length >= 1)
                        InviteListByUsername(usernameList, optionsGym);
                    if (IDList.Length >= 1)
                        InviteListByMasterID(IDList, optionsGym);
                    if (CodesList.Length >= 1 && inviteListByCodeCoroutine == null)
                        inviteListByCodeCoroutine = MelonCoroutines.Start(InviteListByFriendCode(CodesList, optionsGym));
                    loggerInstance.Msg("People invited!");
                }));
                inviteListButtonGym.transform.SetParent(inviteButtonObjectGym.transform);
                inviteListButtonGym.transform.localPosition = new Vector3(0.0326f, 0.0054f, 0.0458f);
                inviteListButtonGym.transform.rotation = Quaternion.Euler(0, 285, 90);
            }

            if (sceneName != "Park" || !Calls.Players.IsHost()) return;
            // Set up necessary things for inviting
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
            
            // Set up invite button
            GameObject shiftstoneButton = GameObjects.Park.INTERACTABLES.Shiftstones.ShiftstoneQuickswapper.FloatingButton.GetGameObject();
            GameObject inviteButtonObject = GameObject.Instantiate(shiftstoneButton);
            inviteButtonObject.name = "AutoInvite Button";
            inviteButtonObject.transform.position = new Vector3(-29.2742f, -1.6354f, -7.015f);
            inviteButtonObject.transform.rotation = Quaternion.Euler(-0, 90, 0);
            inviteButtonObject.transform.GetChild(1).GetComponent<TextMeshPro>().text = "AutoInvite";
            GameObject.Destroy(inviteButtonObject.transform.GetChild(2).gameObject);
            GameObject inviteListButton = Create.NewButton();
            inviteListButton.name = "InviteListButton";
            inviteListButton.transform.GetChild(0).gameObject.GetComponent<InteractionButton>().onPressed.AddListener(new System.Action(() =>
            {
                UpdateLists();
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
                
                GameObject optionsObject;
                if (currentScene == "Gym")
                {
                    optionsObject = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else if (currentScene == "Park")
                {
                    optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else
                {
                    loggerInstance.Error("Users cannot be invited in scene \"" + currentScene + '"');
                    return;
                }
                
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
                // Find friends with username (strips colour codes)
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
                        if (Calls.Players.GetAllPlayers().ToArray().Where(
                                player => player.Data.GeneralData.PlayFabMasterId == friend.PlayFabMasterId
                                ).Count() != 0) // Avoids inviting someone who's already in a park
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
                GameObject optionsObject;

                if (currentScene == "Gym")
                {
                    optionsObject = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else if (currentScene == "Park")
                {
                    optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else
                {
                    loggerInstance.Error("Users cannot be invited in scene \"" + currentScene + '"');
                    return;
                }
                
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
                    return; // Don't need to invite someone who's already here
                }
                System.Collections.Generic.IEnumerable<GetFriendsResult> friendsWithID = friends.ToArray().Where(friend => friend.PlayFabMasterId == masterID);

                if (friendsWithID.Count() == 0) // Need to fetch the info the inviter function requires
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
                else // Info is already fetched for us
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
                GameObject optionsObject;

                if (currentScene == "Gym")
                {
                    optionsObject = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else if (currentScene == "Park")
                {
                    optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else
                {
                    loggerInstance.Error("Users cannot be invited in scene \"" + currentScene + '"');
                    yield break;
                }
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

            GameObject friendScreen;
            if (currentScene == "Gym")
            {
                friendScreen = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.PlayerFinderScreen.GetGameObject();
            }
            else if (currentScene == "Park")
            {
                friendScreen = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.PlayerFinderScreen.GetGameObject();
            }
            else
            {
                loggerInstance.Error("Users cannot be invited in scene \"" + currentScene + '"');
                yield break;
            }
            FindUser findUser = friendScreen.GetComponent<FindUser>();

            foreach (string code in codes)
            {
                if (codeCache.ContainsKey(code)) // Caching means pressing the invite button again won't re-fetch the same players (slow)
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
                for (int i = 1; i <= 8; i++) // Clear the code in case the search failed
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
                GameObject optionsObject;
                if (Core.currentScene == "Gym")
                {
                    optionsObject = GameObjects.Gym.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else if (Core.currentScene == "Park")
                {
                    optionsObject = GameObjects.Park.INTERACTABLES.Telephone20REDUXspecialedition.SettingsScreen.GetGameObject();
                }
                else
                {
                    Core.loggerInstance.Error("Users cannot be invited in scene \"" + Core.currentScene + '"');
                    return;
                }
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