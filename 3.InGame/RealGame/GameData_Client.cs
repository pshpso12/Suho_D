using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;

[System.Serializable]
public class GameItem
{
    public bool PlayerExist;
    public bool FakeHost;
    public string PlayerNickname;
    public int PlayerLevel;
    public byte[] PlayerTexture;
    public string PlayerMainCharacterID;
    public string PlayerTopOutfitID;
    public string PlayerBottomOutfitID;
    public string PlayerShoesOutfitID;
    public string PlayerAllInOneOutfitID;
    public bool PConnection;
}
[System.Serializable]
public class LootItem
{
    public string ItemName;
    public int ItemCount;
}
public class GameData_Client : NetworkBehaviour
{
    [SerializeField] private GameObject[] GamecharacterPrefabs;
    [SyncVar]
    public int PlayerIndex;
    [SyncVar]
    public int CurrnetRoomNumber;
    [SyncVar]
    public Guid RoomId;

    public readonly SyncList<GameItem> Gameinventory = new SyncList<GameItem>();
    public readonly SyncList<LootItem> Lootinventory = new SyncList<LootItem>();

    [SyncVar]
    public bool GetCloneData;
    [SyncVar]
    public bool RealSceneChnage;
    public string NetUniqueID;
    public string NetworkAddress;
    public ushort NetworkPort;
    [SyncVar]
    public int ImageIndex;
    
    public List<CharacterCostumeInfo> characterDataList = new List<CharacterCostumeInfo>();
    public Image PanelFade;
    public Image PanelLoad;
    private GameObject RoomObj;
    private Game_Things gamethings;
    
    private IngameTimer ingameTimer;
    public bool F_Death = false;

    [SyncVar(hook = nameof(SetPlayerGold))]
    public int Gold;
    [SyncVar(hook = nameof(SetPlayerGas))]
    public int Gas = 0;
    
    public Upgrade_Fam upgrade_fam;
    public List<Transform> enemyPrefab;
    public List<Transform> BossPrefab;
    public Transform spawnPoint;
    
    [SyncVar(hook = nameof(SetPlayerRound))]
    public int RoundNum = -1;
    public int BossNum = -1;

    public readonly SyncDictionary<int, int> EnemyCounts = new SyncDictionary<int, int>();
    private List<GameObject> characterPools = new List<GameObject>();
    public GameObject CharaterspawnPoint;
    private Select_Change selectchange;
    private UnitsSelection_Net unitselectnet;
    private bool LeaveState = true;
    private bool ToLobby = true;

    [SerializeField] private TMP_Text chatText;

    private List<string> profanitiesList = new List<string>();
    public string lastWhisperID = "";
    private List<string> messageHistory = new List<string>();
    private int currentHistoryIndex = -1;
    private static event Action<string> OnMessage;

    public LootItemDataList lootitemdataList;

    [SerializeField] private List<GameObject> Bag_ItemsList;

    public List<Achievement> achievements;
    public readonly SyncList<Achievement_Sh> Achievementinventory = new SyncList<Achievement_Sh>();
    public List<string> UNameList;
    public List<string> CchatList;
    private List<GameObject> Mission0 = new List<GameObject>();
    private List<GameObject> Mission1 = new List<GameObject>();
    private bool F_Sell = false;
    private string Last_message;
    
    /*계산은 서버측에서 진행 문제 없음*/
    public int[] upgradeCosts_ = { 5, 5, 5, 5, 6, 7, 8, 9, 10, 15, 16, 17, 18, 19, 20, 22, 24, 26, 28, 30, 30, 32, 32, 34, 34, 36, 36, 38, 38, 38};
    public int[] upgradeProbabilities_ = { 80, 75, 70, 65, 60, 55, 50, 45, 40, 35, 30, 25, 20, 15, 10, 9, 8, 7, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1, 1};
    public int[] upgradeA_Num_ = {0, 10, 30, 55, 80, 120, 160, 200, 245, 295, 345, 400, 460, 530, 600, 680, 760, 900, 1100, 1400, 1800, 2300, 2900, 3500, 4200, 4900, 5700, 6500, 7400, 8300, 9200};
    public int[] upgradeS_Num_ = {0, 2, 4, 6, 8, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100, 115, 130, 145, 160, 180, 200, 230, 260, 290};
    public GameObject UpgradeSuccess;
    public GameObject UpgradeFail;

    public bool CBossUpgrade = false;
    public bool CBossUnit = false;

    public BackgroundManager audioManager;

    private float lastMessageTime = 0f;
    private int messageCount = 0;
    private float restrictionTime = 30f;
    private int maxMessagesPerSecond = 3;
    private bool messageenabled = true;

    void Start()
    {
        if(isClient)
        {
            DontDestroyOnLoad(this);
            Gameinventory.Callback += OnGameInventoryUpdated;
            Lootinventory.Callback += OnLootInventoryUpdated;
            Achievementinventory.Callback += OnAchievementinventoryUpdated;
            EnemyCounts.Callback += OnEquipmentChange;
            OnMessage += HandleNewMessage;
        }
    }
    
    public void Initialize_RoomGame()
    {
        if(isClient && isOwned)
        {
            PanelFade = GameObject.Find("Panel_Fade").GetComponent<Image>();
            PanelLoad = GameObject.Find("Panel_Loading").GetComponent<Image>();
            /*게임씬의 시작이 로딩창 부터인데 방에서의 로딩창과 동일한 로딩창을 사용하기 위함*/
            PanelLoad.sprite = gamethings.GloadList[ImageIndex];
            GameObject TimerObject = GameObject.Find("Round_board/Timer (TMP)");
            if(TimerObject != null)
                ingameTimer = TimerObject.GetComponent<IngameTimer>();

            for (int i = 0; i < Gameinventory.Count; i++)
            {
                GameITem_OPSET(i, null, Gameinventory[i]);
                OP_EnemyCOunt(i, 0);
            }

            gamethings.Buy_Btn.onClick.AddListener(Buy_listener);
            gamethings.Change_Btn.onClick.AddListener(Change_listener);
            gamethings.HL_Btn.onClick.AddListener(() => HandleUpgrade(gamethings._HL_Num, HL_Btn, 0));
            gamethings.JL_Btn.onClick.AddListener(() => HandleUpgrade(gamethings._JL_Num, JL_Btn, 1));
            gamethings.HD_Btn.onClick.AddListener(() => HandleUpgrade(gamethings._HD_Num, HD_Btn, 2));
            gamethings.JD_Btn.onClick.AddListener(() => HandleUpgrade(gamethings._JD_Num, JD_Btn, 3));

            GameObject PlayerMC = GameObject.Find("M_cc_ex");
            Player Forindex = PlayerMC.GetComponent<Player>();
            Forindex.playerID = PlayerIndex;
            Forindex.CamSet(PlayerIndex);

            GameObject UnitSelect = GameObject.Find("Unit_Select");
            selectchange = UnitSelect.GetComponent<Select_Change>();
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            selectchange.myGamedata = this;
            selectchange.U_Up.onClick.AddListener(Unit_Upgrade_listener);
            selectchange.U_Up_Pre.onClick.AddListener(Unit_UpgradePre_listener);
            selectchange.U_Sell.onClick.AddListener(Unit_Sell_listener);
            CmdDamFam(opponentIdentity);
            unitselectnet = GameObject.Find("M_cc_ex").GetComponent<UnitsSelection_Net>();

            Bag_ItemsList = new List<GameObject>();
            Transform contentTransform = Bag_Paenl.transform.Find("Scroll View/Viewport/Content");
            for (int i = 1; i <= 24; i++)
            {
                Transform itemTransform = contentTransform.Find($"Item_{i}");
                if (itemTransform != null)
                {
                    Bag_ItemsList.Add(itemTransform.gameObject);
                }
            }
            gamethings.Bag_Btn.onClick.AddListener(() => {
                gamethings.Bag_Paenl.SetActive(!Bag_Paenl.activeSelf);
                gamethings.BagLoot_Image.SetActive(false);
                HideTooltip();
            });
            gamethings.Bag_Quit_btn.onClick.AddListener(() => {
                gamethings.Bag_Paenl.SetActive(false);
                HideTooltip();
            });

            gamethings.Quest_Btn.onClick.AddListener(() => {
                gamethings.Quest_Paenl.SetActive(!gamethings.Quest_Paenl.activeSelf);
                gamethings.QuestLoot_Image.SetActive(false);
            });
            gamethings.Quest_Quit_btn.onClick.AddListener(() => {
                gamethings.Quest_Paenl.SetActive(false);
            });
            AchievementReload();


            chatText = chatUI.transform.Find("Panel/Scroll View/Viewport/Content/Text (TMP)").GetComponent<TMP_Text>();
            LoadProfanities();
            gamethings.chatInputField.onEndEdit.AddListener(Send);
            gamethings.chatInputField.onValueChanged.AddListener(delegate { HandleInputFieldChange(); });

            /*게임서버로 오는 중 온 메시지를 출력하기 위함*/
            var ChatRegi = FindObjectOfType<ChatClient>();
            if (ChatRegi != null)
            {
                if (chatText != null && ChatRegi.messageBuffer.Count > 0)
                {
                    chatText.text = string.Concat(ChatRegi.messageBuffer);
                    ChatRegi.messageBuffer.Clear();
                }
            }


            AddButtonListeners(GameObject.Find("Set_set/Panel/cre_btn_1").GetComponent<Button>(), false, true, 3);
            AddButtonListeners(GameObject.Find("Set_set/Panel/cre_btn_2").GetComponent<Button>(), false, true, 2);
            AddButtonListeners(gamethings.Quest_Btn, true, true, 3);
            AddButtonListeners(gamethings.Quest_Quit_btn, false, true, 2);
            AddButtonListeners(gamethings.Bag_Btn, true, true, 3);
            AddButtonListeners(gamethings.Bag_Quit_btn, false, true, 2);
            AddButtonListeners(gamethings.Set_Btn, true, true, 3);

            AddButtonListeners(gamethings.Buy_Btn, true, false, 0);
            AddButtonListeners(gamethings.Change_Btn, true, false, 0);
            AddButtonListeners(gamethings.HL_Btn, true, false, 0);
            AddButtonListeners(gamethings.HD_Btn, true, false, 0);
            AddButtonListeners(gamethings.JL_Btn, true, false, 0);
            AddButtonListeners(gamethings.JD_Btn, true, false, 0);

            AddButtonListeners(selectchange.U_Up, true, false, 0);
            AddButtonListeners(selectchange.U_Up_Pre, true, false, 0);
            AddButtonListeners(selectchange.U_Sell, true, true, 4);
        }
    }
    
    void AddButtonListeners(Button button, bool enableMouseOverSound, bool enableClickSound, int playNextSound = 0)
    {
        if (enableMouseOverSound)
        {
            EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entry.callback.AddListener((eventData) => {
                if (button.interactable)
                {
                    InGameSoundManager.Instance?.MouseOverSound();
                }
            });
            trigger.triggers.Add(entry);
        }
        if (enableClickSound)
        {
            button.onClick.AddListener(() => {
                if (button.interactable)
                {
                    if (playNextSound == 0)
                    {
                        InGameSoundManager.Instance?.UnitSelectSound();
                    }
                    else if (playNextSound == 1)
                    {
                        InGameSoundManager.Instance?.UnitClickSound();
                    }
                    else if (playNextSound == 2)
                    {
                        InGameSoundManager.Instance?.PlayCancelSound();
                    }
                    else if (playNextSound == 3)
                    {
                        InGameSoundManager.Instance?.PlayClick_NoneSound();
                    }
                    else if (playNextSound == 4)
                    {
                        InGameSoundManager.Instance?.USellSound();
                    }
                }
            });
        }
    }

    /*업적이 항상 동일하지 않기 때문에, 두 가지 종류 미션을 좌,우에 나누어서 오브젝트 우선 배치*/
    private void AchievementReload()
    {   
        Transform missions0Transform = Quest_Paenl.transform.Find("ScrollView_0/Viewport/Content");
        Transform missions1Transform = Quest_Paenl.transform.Find("ScrollView_1/Viewport/Content");

        if (missions0Transform != null) {
            foreach (Transform child in missions0Transform) {
                Mission0.Add(child.gameObject);
            }
        }

        if (missions1Transform != null) {
            foreach (Transform child in missions1Transform) {
                Mission1.Add(child.gameObject);
            }
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdGetAchievement(opponentIdentity);
    }
    /*클라이언트 측에서 업적 ADD, SET 시 실행*/
    void OnAchievementinventoryUpdated(SyncList<Achievement_Sh>.Operation op, int index, Achievement_Sh oldItem, Achievement_Sh newItem)
    {
        switch (op)
        {
            case SyncList<Achievement_Sh>.Operation.OP_ADD:
                Achievement_Sh_OPADD(index, oldItem, newItem);
                break;
            case SyncList<Achievement_Sh>.Operation.OP_SET:
                Achievement_Sh_OPSET(index, oldItem, newItem);
                break;
        }
    }
    /*업적이 추가될 때 좌,우에 나누어 이름, 설명, 보상을 출력*/
    void Achievement_Sh_OPADD(int index, Achievement_Sh oldItem, Achievement_Sh newItem)
    {
        if(index < 4)
        {
            TMP_Text M_T = Mission0[index].transform.Find("M_title").GetComponent<TMP_Text>();
            TMP_Text M_D = Mission0[index].transform.Find("M_Des").GetComponent<TMP_Text>();
            TMP_Text M_R = Mission0[index].transform.Find("M_Reward").GetComponent<TMP_Text>();
            M_T.text = newItem.Mission_Name;
            M_D.text = newItem.Mission_Description;
            M_R.text = newItem.Mission_Reward;
        }
        else if(index >= 4)
        {
            TMP_Text M_T = Mission1[index-4].transform.Find("M_title").GetComponent<TMP_Text>();
            TMP_Text M_D = Mission1[index-4].transform.Find("M_Des").GetComponent<TMP_Text>();
            TMP_Text M_R = Mission1[index-4].transform.Find("M_Reward").GetComponent<TMP_Text>();
            M_T.text = newItem.Mission_Name;
            M_D.text = newItem.Mission_Description;
            M_R.text = newItem.Mission_Reward;
        }
    }
    /*업적 완료 시*/
    public void Achievement_Sh_OPSET(int index, Achievement_Sh oldItem, Achievement_Sh newItem)
    {
        if(newItem.achieved)
        {
            if(index < 4)
            {
                TMP_Text M_T = Mission0[index].transform.Find("M_title").GetComponent<TMP_Text>();
                TMP_Text M_D = Mission0[index].transform.Find("M_Des").GetComponent<TMP_Text>();
                TMP_Text M_R = Mission0[index].transform.Find("M_Reward").GetComponent<TMP_Text>();
                Image Done_Im = Mission0[index].transform.Find("Done_Image").GetComponent<Image>();
                /*완료된 업적은 투명도를 낮춰서 구분*/
                float newAlpha = 20f / 255f;
                Color currentColor = M_D.color;
                currentColor.a = newAlpha;
                M_T.color = currentColor;
                M_D.color = currentColor;
                M_R.color = currentColor;
                /*체크 이미지의 알파값을 1로 변경하여 출력*/
                Color imageColor = Done_Im.color;
                imageColor.a = 1f;
                Done_Im.color = imageColor;

                /*UI에 로그를 띄움*/
                ShowMessage("[업적] <color=#C9D3DF>" + newItem.Mission_Name + "</color>업적을 완료하였습니다.", 3f);
                if(InGameSoundManager.Instance != null)
                    InGameSoundManager.Instance.QRewardSound();
            }
            else if(index >= 4)
            {
                TMP_Text M_T = Mission1[index-4].transform.Find("M_title").GetComponent<TMP_Text>();
                TMP_Text M_D = Mission1[index-4].transform.Find("M_Des").GetComponent<TMP_Text>();
                TMP_Text M_R = Mission1[index-4].transform.Find("M_Reward").GetComponent<TMP_Text>();
                Image Done_Im = Mission1[index-4].transform.Find("Done_Image").GetComponent<Image>();
                /*완료된 업적은 투명도를 낮춰서 구분*/
                float newAlpha = 20f / 255f;
                Color currentColor = M_D.color;
                currentColor.a = newAlpha;
                M_T.color = currentColor;
                M_D.color = currentColor;
                M_R.color = currentColor;
                /*체크 이미지의 알파값을 1로 변경하여 출력*/
                Color imageColor = Done_Im.color;
                imageColor.a = 1f;
                Done_Im.color = imageColor;
                
                /*UI에 로그를 띄움*/
                ShowMessage("[업적] <color=#C9D3DF>" + newItem.Mission_Name + "</color>업적을 완료하였습니다.", 3f);
                if(InGameSoundManager.Instance != null)
                    InGameSoundManager.Instance.QRewardSound();
            }

            /*업적 팝업을 안띄운 경우 업적 아이콘에 이미지를 덧씌워 완료 표시*/
            if (gamethings.Quest_Paenl != null && !gamethings.Quest_Paenl.activeSelf)
            {
                gamethings.QuestLoot_Image.SetActive(true);
            }
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name == "gamescence1 2" && isOwned)
        {
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            CmdGetMC(opponentIdentity);
            Initialize_RoomGame();
            StartCoroutine(WaitForSceneInitialization());
        }
    }
    /*씬 변경 완료 시 1.5초 후에 씬 변경이 완료됨을 서버로 알림*/
    private IEnumerator WaitForSceneInitialization()
    {
        yield return new WaitForSeconds(1.5f);
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdSetGameSceneLoaded(opponentIdentity);
    }
    /*유저 정보가 변경 시 클라이언트에서 실행하기 위함*/
    void OnGameInventoryUpdated(SyncList<GameItem>.Operation op, int index, GameItem oldItem, GameItem newItem)
    {
        switch (op)
        {
            case SyncList<GameItem>.Operation.OP_SET:
                GameITem_OPSET(index, oldItem, newItem);
                break;
        }
    }
    public void GameITem_OPSET(int index, GameItem oldItem, GameItem newItem)
    {
        /*존재할 경우 닉네임과 이미지를 반영, 연결이 끊어졌을 경우도 UI_DisCon으로 표기*/
        if(newItem.PlayerExist == true)
        {
            gamethings.playerUIElementsgame[index].UI_Name.text = newItem.PlayerNickname;
            if (newItem.PlayerTexture != null && newItem.PlayerTexture.Length > 0)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(newItem.PlayerTexture);
                gamethings.playerUIElementsgame[index].UI_Image.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            if(newItem.PConnection == true)
                gamethings.playerUIElementsgame[index].UI_DisCon.SetActive(false);
            else if(newItem.PConnection == false)
                gamethings.playerUIElementsgame[index].UI_DisCon.SetActive(true);
        }
        else if(newItem.PlayerExist == false)
        {
            gamethings.playerUIElementsgame[index].UI_Player.SetActive(false);
        }
    }
    /*유저의 현재 적의 숫자를 클라이언트에서 실행하기 위함*/
    void OnEquipmentChange(SyncDictionary<int, int>.Operation op, int key, int item)
    {
        switch (op)
        {
            case SyncIDictionary<int, int>.Operation.OP_ADD:
                OP_EnemyCOunt(key, item);
                break;
            case SyncIDictionary<int, int>.Operation.OP_SET:
                OP_EnemyCOunt(key, item);
                break;
        }
    }
    /*적의 개수를 표시*/
    void OP_EnemyCOunt(int key, int item)
    {
        gamethings.playerUIElementsgame[key].UI_Heart.text = item.ToString();
        gamethings.playerUIElementsgame[key].UI_Slider.value = (float)item / 55f;
    }
    /*아이템을 획득 시 클라이언트에서 실행하기 위함*/
    void OnLootInventoryUpdated(SyncList<LootItem>.Operation op, int index, LootItem oldItem, LootItem newItem)
    {
        switch (op)
        {
            case SyncList<LootItem>.Operation.OP_ADD:
                LootITem_OPADD(index, oldItem, newItem);
                break;
            case SyncList<LootItem>.Operation.OP_SET:
                LootITem_OPSET(index, oldItem, newItem);
                break;
        }
    }
    /*아이템을 처음 획득 시*/
    private void LootITem_OPADD(int index, LootItem oldItem, LootItem newItem)
    {
        /*텍스트, 이미지, 테두리를 배치*/
        GameObject BItem = Bag_ItemsList[index];
        Image BItemImage = BItem.transform.Find("Image").GetComponent<Image>();
        TMP_Text BItemText = BItem.GetComponentInChildren<TMP_Text>();
        BItemImage.enabled = true;

        Sprite BitemSprite = Resources.Load<Sprite>($"LootImages/{newItem.ItemName}");
        BItemImage.sprite = BitemSprite;
        BItemText.text = newItem.ItemCount.ToString();

        /*해당 아이템에 PointerEnter 시 툴팁을 표시하고 벗어났을 때 끄기 위함*/
        EventTrigger trigger = BItem.GetComponent<EventTrigger>() ?? BItem.AddComponent<EventTrigger>();

        EventTrigger.Entry entryEnter = new EventTrigger.Entry();
        entryEnter.eventID = EventTriggerType.PointerEnter;
        entryEnter.callback.AddListener((eventData) => { ShowTooltip(newItem.ItemName); });
        trigger.triggers.Add(entryEnter);

        EventTrigger.Entry entryExit = new EventTrigger.Entry();
        entryExit.eventID = EventTriggerType.PointerExit;
        entryExit.callback.AddListener((eventData) => { HideTooltip(); });
        trigger.triggers.Add(entryExit);

        /*가방 팝업을 안 열었을 때, 아이콘 이미지에 덧씌워 획득 표시*/
        if (gamethings.Bag_Paenl != null && !gamethings.Bag_Paenl.activeSelf)
        {
            gamethings.BagLoot_Image.SetActive(true);
        }
        ForLogging(newItem.ItemName);
    }
    /*동일한 아이템을 추가 획득 시*/
    public void LootITem_OPSET(int index, LootItem oldItem, LootItem newItem)
    {
        GameObject BItem = Bag_ItemsList[index];
        TMP_Text BItemText = BItem.GetComponentInChildren<TMP_Text>();
        /*숫자만 변경하여 표시*/
        BItemText.text = newItem.ItemCount.ToString();
        
        /*가방 팝업을 안 열었을 때, 아이콘 이미지에 덧씌워 획득 표시*/
        if (gamethings.Bag_Paenl != null && !gamethings.Bag_Paenl.activeSelf)
        {
            gamethings.BagLoot_Image.SetActive(true);
        }
        ForLogging(newItem.ItemName);
    }

    /*아이템에 등급별로 로그 시에 색상을 다르게 구별*/
    void ForLogging(string itemName)
    {
        LootItemData itemData = System.Array.Find(lootitemdataList.LootItems, item => item.ItemName == itemName);
        if (itemData != null)
        {
            string colorCode = "";
            switch (itemData.ItemType)
            {
                case 0:
                    colorCode = "#E0E0E0";
                    break;
                case 1:
                    colorCode = "#5BBB41";
                    break;
                case 2:
                    colorCode = "#D24444";
                    break;
                case 3:
                    colorCode = "#C1D12A";
                    break;
                case 4:
                    colorCode = "#FFC45E";
                    break;
                default:
                    colorCode = "#FFFFFF";
                    break;
            }
            string Itemmessage = $"<color={colorCode}>[{itemData.ItemName_Show}]</color>를 획득하였습니다!";
            ShowMessage(Itemmessage, 3f);
            if(InGameSoundManager.Instance != null)
                InGameSoundManager.Instance.LootItemSound();
        }
    }
    /*서버측에서 라운드가 변경 될 때 클라이언트에서 표시*/
    void SetPlayerRound(int oldRound, int newRound)
    {
        if(gamethings._RoundNum)
            gamethings._RoundNum.text = (newRound + 1).ToString();
    }
    /*서버측에서 유저의 골드가 변경될 때 클라이언트에서 표시*/
    void SetPlayerGold(int oldGold, int newGold)
    {
        if(gamethings._Gold_Num)
            gamethings._Gold_Num.text = newGold.ToString();

        /*골드가 100 미만일 때 값이 고정인 구매, 교환 버튼 기능 비활성화*/
        if (GetGold() < 100)
        {
            gamethings.Buy_Btn.interactable = false;
            gamethings.Change_Btn.interactable = false;
        }
        else
        {
            /*협동보스를 잡아 보유할 수 있는 유닛이 5개 일 경우*/
            if(CBossUnit)
            {
                if(Globals_Net.CurrentExist_UNITS.Count < 5)
                    gamethings.Buy_Btn.interactable = true;
                else if(Globals_Net.CurrentExist_UNITS.Count >= 5)
                    gamethings.Buy_Btn.interactable = false;
            }
            /*협동보스를 못 잡아 보유할 수 있는 유닛이 4개 일 경우*/
            else
            {
                if(Globals_Net.CurrentExist_UNITS.Count < 4)
                    gamethings.Buy_Btn.interactable = true;
                else if(Globals_Net.CurrentExist_UNITS.Count >= 4)
                    gamethings.Buy_Btn.interactable = false;   
            }
            gamethings.Change_Btn.interactable = true;
        }
            
    }
    /*서버측에서 유저의 가스가 변경될 때 클라이언트에서 표시*/
    void SetPlayerGas(int oldGas, int newGas)
    {
        if(gamethings._Gas_Num)
            gamethings._Gas_Num.text = newGas.ToString();
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*서버측에 업그레이드 버튼을 활성화해도 되는지 확인함 - 서버에 보내지 않고 확인하게 변경 필요*/
        UpdateUpgradeButtonState(opponentIdentity, PlayerIndex, 0);
        UpdateUpgradeButtonState(opponentIdentity, PlayerIndex, 1);
        UpdateUpgradeButtonState(opponentIdentity, PlayerIndex, 2);
        UpdateUpgradeButtonState(opponentIdentity, PlayerIndex, 3);
    }
    /*유닛 구매 버튼 클릭 시*/
    void Buy_listener()
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*현재 고르가 100 이상일 경우 서버로 요청*/
        float currentGold = Gold;
        if(currentGold >= 100)
        {
            CmdBuy_listener(opponentIdentity);
        }
        else
        {
            ShowMessage("자원이 부족합니다.", 3f);
            if(InGameSoundManager.Instance != null)
                InGameSoundManager.Instance.NotEnoughtSound();
        }
    }
    /*유닛 업그레이드 버튼 클릭 시*/
    void Unit_Upgrade_listener()
    {
        if (Globals_Net.SELECTED_UNITS.Count == 1)
        {
            GameObject selectedUnit = Globals_Net.SELECTED_UNITS[0].gameObject;
            if(selectedUnit != null)
            {
                /*유닛의 업그레이드에 따라 필요한 골드가 다르기 때문에 이를 확인*/
                UnitManager_Net selectedManager = selectedUnit.GetComponent<UnitManager_Net>();
                if (selectedManager.UnitUpgradeValue < upgradeCosts_.Length)
                {
                    int requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue];
                    float currentGold = Gold;
                    if(currentGold >= requiredGoldForUpgrade)
                    {
                        Unit_Upgrade(selectedUnit);
                    }
                    else
                    {
                        ShowMessage("자원이 부족합니다.", 3f);
                        if(InGameSoundManager.Instance != null)
                            InGameSoundManager.Instance.NotEnoughtSound();
                    }
                }
            }
        }
    }
    /*보호 강화 클릭 시(보호 강화는 하락이나 파괴가 없지만, 비쌈)*/
    void Unit_UpgradePre_listener()
    {
        if (Globals_Net.SELECTED_UNITS.Count == 1)
        {
            GameObject selectedUnit = Globals_Net.SELECTED_UNITS[0].gameObject;
            if(selectedUnit != null)
            {
                UnitManager_Net selectedManager = selectedUnit.GetComponent<UnitManager_Net>();
                if (selectedManager.UnitUpgradeValue < upgradeCosts_.Length)
                {
                    int requiredGoldForUpgrade;
                    /*10강 까지는 보호강화와 일반강화 모두 동일한 가격*/
                    if(selectedManager.UnitUpgradeValue < 10 )
                        requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue];
                    /*20강 까지는 보호강화와 일반강화의 3배*/
                    else if(selectedManager.UnitUpgradeValue < 20 )
                        requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue] * 3;
                    /*20강 까지는 보호강화와 일반강화의 5배*/
                    else
                        requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue] * 5;
                    float currentGold = Gold;
                    if(currentGold >= requiredGoldForUpgrade)
                    {
                        Unit_UpgradePre(selectedUnit);
                    }
                    else
                    {
                        ShowMessage("자원이 부족합니다.", 3f);
                        if(InGameSoundManager.Instance != null)
                            InGameSoundManager.Instance.NotEnoughtSound();
                    }
                }
            }
        }
    }
    /*유닛 판매 버튼 클릭 시*/
    void Unit_Sell_listener()
    {
        GameObject selectedUnit = Globals_Net.SELECTED_UNITS[0].gameObject;
        /*현재 선택한 유닛 목록에서 해당 유닛 삭제*/
        unitselectnet._RemoveUnitinGroup(selectedUnit.GetComponent<UnitManager_Net>());
        /*보유 중인 유닛 목록에서 해당 유닛 삭제*/
        Globals_Net.CurrentExist_UNITS.Remove(selectedUnit);
        Globals_Net.SELECTED_UNITS.Remove(selectedUnit.GetComponent<UnitManager_Net>());
        /*구매 버튼 위의 유닛 텍스트를 변경*/
        if(CBossUnit)
        {
            gamethings.CuUnitCount.text = Globals_Net.CurrentExist_UNITS.Count + " / 5";
        }
        else
        {
            gamethings.CuUnitCount.text = Globals_Net.CurrentExist_UNITS.Count + " / 4";
        }
        Unit_Sell(selectedUnit);
    }
    /*교환 버튼 클릭 시*/
    void Change_listener()
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        float currentGold = Gold;
        if(currentGold >= 100)
        {
            CmdChange_listener(opponentIdentity);
        }
        else
        {
            ShowMessage("자원이 부족합니다.", 3f);
            if(InGameSoundManager.Instance != null)
                InGameSoundManager.Instance.NotEnoughtSound();
        }
    }
    /*종족 강화 버튼 클릭 시*/
    void HandleUpgrade(TMP_Text upgradeText, Button upgradeButton, int index)
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        if(Gas >= (int)selectchange.upgradeValues[PlayerIndex, index] + 1)
        {
            CmdHandleUpgrade(opponentIdentity, PlayerIndex, index);
            CmdDamFam(opponentIdentity);
        }
        else
        {
            ShowMessage("자원이 부족합니다.", 3f);
            if(InGameSoundManager.Instance != null)
                InGameSoundManager.Instance.NotEnoughtSound();
        }
    }
    /*메시지를 로그에 입력합니다.*/
    public void ShowMessage(string message, float duration)
    {
        GameObject newText = Instantiate(_Log_Text, _Log_Cons.transform);
        newText.GetComponent<TMP_Text>().text = message;
        StartCoroutine(FadeAndDestroy(newText.GetComponent<TMP_Text>(), duration));
    }
    /*메시지는 2초에 걸쳐 희미해지고 삭제합니다.*/
    IEnumerator FadeAndDestroy(TMP_Text text, float delay)
    {
        yield return new WaitForSeconds(delay);

        Color originalColor = text.color;
        float fadeDuration = 2f;
        float timer = 0;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, timer / fadeDuration);
            text.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        Destroy(text.gameObject);
    }
    /*가방에서 아이템 PointerEnter시 아이템 툴팁을 표시*/
    void ShowTooltip(string itemName)
    {
        LootItemData itemData = System.Array.Find(lootitemdataList.LootItems, item => item.ItemName == itemName);
        if (itemData != null)
        {
            gamethings.tooltipImage.sprite = itemData.image;
            gamethings.tooltipNameText.text = itemData.ItemName_Show;
            gamethings.tooltipDesText.text = itemData.ItemDespaction_Shoe;

            string colorCode = "";
            switch (itemData.ItemType)
            {
                case 0:
                    colorCode = "#E0E0E0";
                    break;
                case 1:
                    colorCode = "#5BBB41";
                    break;
                case 2:
                    colorCode = "#D24444";
                    break;
                case 3:
                    colorCode = "#C1D12A";
                    break;
                case 4:
                    colorCode = "#FFC45E";
                    break;
                default:
                    colorCode = "#FFFFFF";
                    break;
            }
            
            if (ColorUtility.TryParseHtmlString(colorCode, out Color color))
            {
                gamethings.tooltipNameText.color = color;
            }

            gamethings.tooltipPanel.SetActive(true);
        }
    }
    /*툴팁 숨기기*/
    void HideTooltip()
    {
        gamethings.tooltipPanel.SetActive(false);
    }
    void Update()
    {
        if (!isClient) return;
        /*툴팁 마우스 위치에 따라 움직이게 하기 위함*/
        if (gamethings.tooltipPanel != null && gamethings.tooltipPanel.activeSelf)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (tooltipPanel.transform.parent as RectTransform),
                Input.mousePosition, 
                null,
                out localPoint
            );
            gamethings.tooltipPanel.transform.localPosition = localPoint;
        }

        /*채팅을 안칠 때, 단축키 지정*/
        if (gamethings.chatInputField != null && !gamethings.chatInputField.isFocused)
        {
            if(Input.GetKeyDown(KeyCode.Q))
            {
                Buy_listener();
            }
            if(Input.GetKeyDown(KeyCode.W))
            {
                Change_listener();
            }
            if (Input.GetKeyDown(KeyCode.Z))
            {
                HandleUpgrade(_HL_Num, HL_Btn, 0);
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                HandleUpgrade(_JL_Num, JL_Btn, 1);
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                HandleUpgrade(_HD_Num, HD_Btn, 2);
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                HandleUpgrade(_JD_Num, JD_Btn, 3);
            }
            if (Input.GetKeyDown(KeyCode.U))
            {
                gamethings.Quest_Paenl.SetActive(!Quest_Paenl.activeSelf);
                gamethings.QuestLoot_Image.SetActive(false);
                if(InGameSoundManager.Instance != null)
                    InGameSoundManager.Instance.PlayClick_NoneSound();
            }
            if (Input.GetKeyDown(KeyCode.I))
            {
                gamethings.Bag_Paenl.SetActive(!Bag_Paenl.activeSelf);
                gamethings.BagLoot_Image.SetActive(false);
                HideTooltip();
                if(InGameSoundManager.Instance != null)
                    InGameSoundManager.Instance.PlayClick_NoneSound();
            }
            if(Input.GetKeyDown(KeyCode.D))
            {
                Unit_Upgrade_listener();
            }
            if(Input.GetKeyDown(KeyCode.F))
            {
                Unit_UpgradePre_listener();
            }
        }
    }
