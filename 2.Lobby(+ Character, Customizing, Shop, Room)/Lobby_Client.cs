using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System;
using UnityEngine.EventSystems;
using System.Linq;
using Newtonsoft.Json;

public class Lobby_Client : NetworkBehaviour
{
   
    private LobbyBase lobbybase;
    private Lobby_Things lobbythings;
    private CharBase charbase;
    private Char_Things charthings;
    private CusBase cusbase;
    private Cus_Things custhings;
    private ShopBase shopbase;
    private Shop_Things shopthings;
    private Room_Things roomthings;
    private Purchases_Things purchasethings;
    
    private List<string> profanitiesList = new List<string>();
    private List<string> messageBuffer = new List<string>();
    private const int maxMessageCount = 30;

    private TMP_Text chatText;
    private TMP_Text RoomchatText;
    
    private static event Action<string> OnMessage;
    private static event Action<string> OnRoomMessage;
    private string lastWhisperID = "";
    private List<string> messageHistory = new List<string>();
    private int currentHistoryIndex = -1;

    [SerializeField] private Sprite Cpimage;
    [SerializeField] private Sprite Bpimage;
    [SerializeField] private OutfitDataList outfitDataList;

    private int Shop_characterNum;
    private string Shop_type;
    private string Shop_description;
    private string Shop_itemName;
    private string Shop_price;
    private string Shop_priceType;
    private bool Shop_isWorn;
    
    private Callback<MicroTxnAuthorizationResponse_t> microTxnCallback;

    public BackgroundManager audioManager;
    public UISoundManager UisoundManager;
    [SerializeField] private Texture2D cursorTexture;

    private float lastMessageTime = 0f;
    private int messageCount = 0;
    private float restrictionTime = 30f;
    private int maxMessagesPerSecond = 3;
    private bool messageenabled = true;
    
    void Start()
    {
        /*event와 Callback 등록*/
        OnMessage += HandleNewMessage;
        microTxnCallback = Callback<MicroTxnAuthorizationResponse_t>.Create(OnMicroTxnAuthorizationResponse);
    }
    
    public void Initialize()
    {
        if(isClient && isLocalPlayer)
        {
            /*GameServer에서 Lobby로 이동 시 저장된 데이터 삭제*/
            Clone_Datas CloneDatas_22 = FindObjectOfType<Clone_Datas>();
            if(CloneDatas_22 != null)
                Destroy(CloneDatas_22.gameObject);

     	    lobbythings = GameObject.Find("Lobby_Object").GetComponent<Lobby_Things>();
	    chatText = chatUI.transform.Find("Panel/Scroll View/Viewport/Content/Text (TMP)").GetComponent<TMP_Text>();
            /*비속어 불러오기*/
            LoadProfanities();

            
            lobbythings.chatInputField.onEndEdit.AddListener(Send);
            /*"/r"로 다시 보내는 기능*/
            lobbythings.chatInputField.onValueChanged.AddListener(delegate { HandleInputFieldChange(); });

            lobbythings.RoomCreateButton.onClick.AddListener(OnButtonSendRoom);

            /*결제 기능 적용*/
            PurChaseLoad();

            /*채팅 기능이 없는 커스터마이징, 상점에서 다시 로비로 올 때 그 사이에 온 메시지 입력 (최신 순으로 최대 30개)*/
            if (chatText != null && messageBuffer.Count > 0)
            {
                chatText.text = string.Concat(messageBuffer);
                messageBuffer.Clear();
            }
            /*방 목록 찾기와 새로고침 적용*/
            GameObject ForRoomCon = GameObject.Find("Scenmanager");
            if(ForRoomCon != null)
            {
                lobbybase = ForRoomCon.GetComponent<LobbyBase>();
                if(lobbybase != null)
                {
                    lobbybase.onReLoadRoom.AddListener(ReLoadRooms);
                    lobbybase.onSendRoom.AddListener(SendRoom);
                }
            }
            /*초기 로비 이동 시 방 목록 새로고침*/
            ReLoadRooms();

            if (cursorTexture != null)
            {
                Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
            }

            if(audioManager != null)
            {
                audioManager.StopIngameMusic();
                if(!audioManager.IsMusicBackPlaying())
                {
                    audioManager.PlayBackMusic();
                }
                if(!audioManager.IsMusicPlaying())
                {
                    audioManager.PlayMusic_A();
                }
            }
            else
            {
                audioManager = GameObject.Find("SoundObject").GetComponent<BackgroundManager>();
                audioManager.ToLobbyMusic();
                if(!audioManager.IsMusicBackPlaying())
                {
                    audioManager.PlayBackMusic();
                }
            }
            
            UisoundManager = GameObject.Find("UI_SoundObject").GetComponent<UISoundManager>();
            Button Schange_chbtn1 = GameObject.Find("Ui_Underone/Btn_im").GetComponent<Schange_Script>().ch_btn1;
            Button Schange_chbtn2 = GameObject.Find("Ui_Underone/Btn_im").GetComponent<Schange_Script>().ch_btn2;
            Button Schange_chbtn3 = GameObject.Find("Ui_Underone/Btn_im").GetComponent<Schange_Script>().ch_btn3;
            Button SQuitBtn = GameObject.Find("Ui_Overone/Over_Btn/Quit_btn").GetComponent<Button>();
            Button SSetBtn = GameObject.Find("Ui_Overone/Over_Btn/Set_btn").GetComponent<Button>();
            Button UiCreBtn = GameObject.Find("Ui_cre/Cre_btn").GetComponent<Button>();
            AddButtonListeners(Schange_chbtn1, true, false, 0);
            AddButtonListeners(Schange_chbtn2, true, false, 0);
            AddButtonListeners(Schange_chbtn3, true, false, 0);
            AddButtonListeners(lobbybase.RoomSearchButton, true, true, 0);
            AddButtonListeners(lobbybase.RoomReloadButton, true, true, 0);
            AddButtonListeners(lobbybase.RoomleftButton, true, true, 0);
            AddButtonListeners(lobbybase.RoomrightButton, true, true, 0);
            AddButtonListeners(UiCreBtn, true, true, 1);
            AddButtonListeners(SQuitBtn, true, true, 1);
            AddButtonListeners(SSetBtn, true, true, 1);

            AddButtonListeners(lobbythings.RoomCreateButton, false, true, 3);
            AddButtonListeners(lobbythings.RoomSetUI.transform.Find("Panel/cre_btn_2").GetComponent<Button>(), false, true, 2);
            AddButtonListeners(lobbythings.Room_Exit_Forced.transform.Find("Panel/Buy_success_btn").GetComponent<Button>(), false, true, 3);
            AddButtonListeners(lobbythings.Room_Enter_Error_Passwrong.transform.Find("Panel/Buy_success_btn").GetComponent<Button>(), false, true, 3);
            AddButtonListeners(lobbythings.Room_Enter_Withpass_Panl.transform.Find("Panel/enter_btn_1").GetComponent<Button>(), false, true, 3);
            AddButtonListeners(lobbythings.Room_Enter_Withpass_Panl.transform.Find("Panel/enter_btn_2").GetComponent<Button>(), false, true, 2);
            AddButtonListeners(lobbythings.Room_Enter_Error_Panl.transform.Find("Panel/Buy_success_btn").GetComponent<Button>(), false, true, 3);

            AddButtonListeners(GameObject.Find("Canvas/Quit_set/Panel/cre_btn_1").GetComponent<Button>(), false, true, 3);
            AddButtonListeners(GameObject.Find("Canvas/Quit_set/Panel/cre_btn_2").GetComponent<Button>(), false, true, 2);

            AddButtonListeners(GameObject.Find("Canvas/Set_set/Panel/cre_btn_1").GetComponent<Button>(), false, true, 3);
            AddButtonListeners(GameObject.Find("Canvas/Set_set/Panel/cre_btn_2").GetComponent<Button>(), false, true, 2);
        }
    }
    
    public void Initialize_Char()
    {
        if(isClient && isLocalPlayer)
        {
            charbase = charthings.Ui_List.GetComponent<CharBase>();
            charthings.Change_Btn.onClick.AddListener(OnButtonSendIndex);

            PurChaseLoad();

            Button SQuitBtn = GameObject.Find("Ui_Overone/Over_Btn/Quit_btn").GetComponent<Button>();
            foreach (Button btn in charbase.cha_buttons)
            {
                AddButtonListeners(btn, true, true, 0);
            }
            AddButtonListeners(charthings.Change_Btn, true, true, 3);
            AddButtonListeners(charthings.Log_panel_btn, false, true, 3);
            AddButtonListeners(charthings.Log_panel_fail_btn, false, true, 3);
            AddButtonListeners(SQuitBtn, true, true, 0);

            if(audioManager != null)
            {
                audioManager.StopIngameMusic();
                if(audioManager.IsMusicBackPlaying())
                {
                    audioManager.StopBackMusic();
                }
                if(!audioManager.IsMusicPlaying())
                {
                    audioManager.PlayMusic_A();
                }
            }
        }
    }

    public void Initialize_Cus()
    {
        if(isClient && isLocalPlayer)
        {
            GameObject levelListGameObject = GameObject.Find("Charater_List/Scroll View/Viewport");
            if(levelListGameObject != null)
            {
                CusBase cusbase = levelListGameObject.GetComponent<CusBase>();
                if(cusbase != null)
                {
                    cusbase.onSendCloth.AddListener(HandleSendCloth);

                    AddButtonListeners(cusbase.Lpage_Btn, true, true, 0);
                    AddButtonListeners(cusbase.Rpage_Btn, true, true, 0);
                    foreach(Button cbtn in cusbase.buttons)
                    {
                        AddButtonListeners(cbtn, true, true, 0);
                    }
                }
            }

            PurChaseLoad();

            Button SQuitBtn = GameObject.Find("Ui_Overone/Over_Btn/Quit_btn").GetComponent<Button>();
            CusCharacter cuscharacter = GameObject.Find("Scenmanager").GetComponent<CusCharacter>();
            if(cuscharacter)
            {
                foreach (Button btn in cuscharacter.ChaButtons)
                {
                    AddButtonListeners(btn, true, true, 0);
                }
            }
            AddButtonListeners(custhings.Cus_Log_panel_fail_btn, false, true, 3);
            AddButtonListeners(SQuitBtn, true, true, 0);

            if(audioManager != null)
            {
                audioManager.StopIngameMusic();
                if(audioManager.IsMusicBackPlaying())
                {
                    audioManager.StopBackMusic();
                }
                if(!audioManager.IsMusicPlaying())
                {
                    audioManager.PlayMusic_A();
                }
            }
        }
    }

    public void Initialize_Shop()
    {
        if(isClient && isLocalPlayer)
        {
            GameObject levelListGameObject = GameObject.Find("Charater_List/Scroll View/Viewport");
            if(levelListGameObject != null)
            {
                shopbase = levelListGameObject.GetComponent<ShopBase>();
                if(shopbase != null)
                {
                    shopbase.onSendItem.AddListener(HandleSendItem);

                    foreach(Button cbtn in shopbase.buttons)
                    {
                        AddButtonListeners(cbtn, true, true, 0);
                    }
                }
            }
            shopthings.Shop_dropdown.onValueChanged.AddListener(delegate {
                DropdownValueChanged(shopthings.Shop_dropdown);
            });
            shopthings.Shop_BuySuccess.onClick.AddListener(() => 
            OnButtonItemBuy(Shop_characterNum, Shop_type, Shop_description, Shop_itemName, 
            Shop_price, Shop_priceType, Shop_isWorn));
            shopthings.Shop_toggle.onValueChanged.AddListener(ToggleValueChanged);

            Button SQuitBtn = GameObject.Find("Ui_Overone/Over_Btn/Quit_btn").GetComponent<Button>();
            Button Shop_BuyNo1 = buy_Panel.transform.Find("Button").GetComponent<Button>();
            Button Shop_BuyNo2 = buy_Panel.transform.Find("Buy_success_btn_no").GetComponent<Button>();
            Button Shop_BuyYes = buy_Panel.transform.Find("Buy_success_btn_yes").GetComponent<Button>();
            ShopCharacter shopcharacter = GameObject.Find("Scenmanager").GetComponent<ShopCharacter>();

            PurChaseLoad();
            
            if(shopcharacter)
            {
                AddButtonListeners(shopcharacter.ReloadBtn, true, true, 0);
                foreach (Button btn in shopcharacter.ChaButtons)
                {
                    AddButtonListeners(btn, true, true, 0);
                }
            }
            AddButtonListeners(Shop_BuyYes, false, true, 3);
            AddButtonListeners(Shop_BuyNo1, false, true, 2);
            AddButtonListeners(Shop_BuyNo2, false, true, 2);
            AddButtonListeners(shopthings.Shop_BuySuccess, false, true, 3);
            AddButtonListeners(buy_Panel.transform.Find("BuyCheck_Panel/Buy_success/Panel/LastBuy_success_btn_no").GetComponent<Button>(), false, true, 2);
            AddButtonListeners(shopthings.Shop_Log_Success_btn, false, true, 3);
            AddButtonListeners(shopthings.Shop_Log_Fail_btn, false, true, 3);
            AddButtonListeners(shopthings.Shop_Log_Fail_btn2, false, true, 3);
            AddButtonListeners(SQuitBtn, true, true, 0);
            
            if(audioManager != null)
            {
                audioManager.StopIngameMusic();
                if(audioManager.IsMusicBackPlaying())
                {
                    audioManager.StopBackMusic();
                }
                if(!audioManager.IsMusicPlaying())
                {
                    audioManager.PlayMusic_A();
                }
            }
        }
    }
    
    public void Initialize_Room()
    {
        if(isClient && isLocalPlayer)
        {
            roomthings.RoomchatInputField.onEndEdit.AddListener(RoomChatSend);
            roomthings.RoomchatInputField.onValueChanged.AddListener(delegate { HandleRoomInputFieldChange(); });
            RoomchatText = RoomchatUI.transform.Find("Panel/Scroll View/Viewport/Content/Text (TMP)").GetComponent<TMP_Text>();
            if (cursorTexture != null)
            {
                Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
            }
            
            if(audioManager != null)
            {
                audioManager.StopIngameMusic();
                if(!audioManager.IsMusicBackPlaying())
                {
                    audioManager.PlayBackMusic();
                }
                if(!audioManager.IsMusicPlaying())
                {
                    audioManager.PlayMusic_A();
                }
            }
            else
            {
                audioManager = GameObject.Find("SoundObject").GetComponent<BackgroundManager>();
                audioManager.ToLobbyMusic();
                if(!audioManager.IsMusicBackPlaying())
                {
                    audioManager.PlayBackMusic();
                }
            }
        }
    }

    void PurChaseLoad()
    {
        List<int> PurchaseList = new List<int> { 0, 0, 0, 0, 0};
	purchasethings = GameObject.Find("purchase_Object").GetComponent<Purchases_Things>();

        int totalCost = 0;

 	/*충전 버튼 클릭 시 총합을 0으로 설정*/
        purchasethings.Purchases_Button.onClick.AddListener(() => {
            PurchaseList = new List<int> { 0, 0, 0, 0, 0};
            totalCost = 0;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
            purchasethings.Purchase_MainPanel.SetActive(true);
        });

	/*각 버튼에 따라 값 추가*/
        purchasethings.Purchases_Button1000.onClick.AddListener(() => {
            PurchaseList[0] += 1;
            totalCost += 1000;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
        });

        purchasethings.Purchases_Button5000.onClick.AddListener(() => {
            PurchaseList[1] += 1;
            totalCost += 5000;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
        });

        purchasethings.Purchases_Button10000.onClick.AddListener(() => {
            PurchaseList[2] += 1;
            totalCost += 10000;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
        });

        purchasethings.Purchases_Button50000.onClick.AddListener(() => {
            PurchaseList[3] += 1;
            totalCost += 50000;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
        });

        purchasethings.Purchases_Button100000.onClick.AddListener(() => {
            PurchaseList[4] += 1;
            totalCost += 100000;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
        });

 	/*Reset시 값 초기화*/
        purchasethings.Purchases_ButtonReset.onClick.AddListener(() => {
            PurchaseList = new List<int> { 0, 0, 0, 0, 0 };
            totalCost = 0;
            purchasethings.Purchase_textCost.text = totalCost.ToString("N0");
        });
        
        Button PurChaseCheck = purchasethings.Purchase_Panel.transform.Find("Buy_success_btn_yes").GetComponent<Button>();

	/*결제 요청 시 PurchaseList값을 합해서 전송*/
	PurChaseCheck.onClick.AddListener(() => {
            if(totalCost != 0)
            {
                purchasethings.PurchaseCheckLast_Panel.SetActive(true);
                NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
                CSteamID steamID = SteamUser.GetSteamID();
                int Sumpurchase = CalculateSumPurchase(PurchaseList);
                CmdPurcahseRe(opponentIdentity, steamID.ToString(), Sumpurchase);
            }
        });

        purchasethings.Purchase_BuySuccess.onClick.AddListener(() => {
            purchasethings.PurchaseCheckReal_Panel.SetActive(false);
            purchasethings.PurchaseCheckRealFail_Panel.SetActive(false);
            purchasethings.PurchaseCheckLast_Panel.SetActive(false);
            purchasethings.Purchase_MainPanel.SetActive(false);
        });
        purchasethings.Purchase_BuyFail.onClick.AddListener(() => {
            purchasethings.PurchaseCheckReal_Panel.SetActive(false);
            purchasethings.PurchaseCheckRealFail_Panel.SetActive(false);
            purchasethings.PurchaseCheckLast_Panel.SetActive(false);
            purchasethings.Purchase_MainPanel.SetActive(false);
        });

        purchasethings.Purchase_Log_Success_btn.onClick.AddListener(() => {
            purchasethings.Purchase_Log_Success.SetActive(false);
            purchasethings.PurchaseCheckReal_Panel.SetActive(false);
            purchasethings.PurchaseCheckRealFail_Panel.SetActive(false);
            purchasethings.PurchaseCheckLast_Panel.SetActive(false);
            purchasethings.Purchase_MainPanel.SetActive(false);
        });

        purchasethings.Purchase_Log_Fail_btn.onClick.AddListener(() => {
            purchasethings.Purchase_Log_Fail.SetActive(false);
            purchasethings.PurchaseCheckReal_Panel.SetActive(false);
            purchasethings.PurchaseCheckRealFail_Panel.SetActive(false);
            purchasethings.PurchaseCheckLast_Panel.SetActive(false);
            purchasethings.Purchase_MainPanel.SetActive(false);
        });

        purchasethings.Purchase_Log_Fail2_btn.onClick.AddListener(() => {
            purchasethings.Purchase_Log_Fail2.SetActive(false);
            purchasethings.PurchaseCheckReal_Panel.SetActive(false);
            purchasethings.PurchaseCheckRealFail_Panel.SetActive(false);
            purchasethings.PurchaseCheckLast_Panel.SetActive(false);
            purchasethings.Purchase_MainPanel.SetActive(false);
        });

        AddButtonListeners(purchasethings.Purchases_Button, false, true, 1);
        AddButtonListeners(purchasethings.Purchases_Button1000, false, true, 0);
        AddButtonListeners(purchasethings.Purchases_Button5000, false, true, 0);
        AddButtonListeners(purchasethings.Purchases_Button10000, false, true, 0);
        AddButtonListeners(purchasethings.Purchases_Button50000, false, true, 0);
        AddButtonListeners(purchasethings.Purchases_Button100000, false, true, 0);
        AddButtonListeners(purchasethings.Purchases_ButtonReset, false, true, 0);

        AddButtonListeners(purchasethings.Purchase_Panel.transform.Find("Buy_success_btn_yes").GetComponent<Button>(), false, true, 3);
        AddButtonListeners(purchasethings.Purchase_Panel.transform.Find("Buy_success_btn_no").GetComponent<Button>(), false, true, 2);
        AddButtonListeners(purchasethings.Purchase_Panel.transform.Find("Button").GetComponent<Button>(), false, true, 2);
        AddButtonListeners(purchasethings.Purchase_BuySuccess, false, true, 3);
        AddButtonListeners(purchasethings.Purchase_Log_Success_btn, false, true, 3);
        AddButtonListeners(purchasethings.Purchase_Log_Fail_btn, false, true, 3);
        AddButtonListeners(purchasethings.Purchase_Log_Fail2_btn, false, true, 3);
    }
    /*PurchaseList값을 합해서 반환*/
    int CalculateSumPurchase(List<int> PurchaseList)
    {
        int[] purchaseAmounts = { 1000, 5000, 10000, 50000, 100000 };
        int Sumpurchase = 0;

        for (int i = 0; i < PurchaseList.Count; i++)
        {
            Sumpurchase += PurchaseList[i] * purchaseAmounts[i];
        }

        return Sumpurchase;
    }
    /*Steam서버로 부터 클라이언트가 결제가 완료됨을 받으면, 서버로 해당 정보를 보냄, 아니라면 결제 실패 팝업 띄움*/
    void OnMicroTxnAuthorizationResponse(MicroTxnAuthorizationResponse_t pCallback) 
    {
        if (pCallback.m_bAuthorized == 1)
        {
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            SendTransactionToServer(opponentIdentity, pCallback.m_ulOrderID);
        }
        else
        {
            if(purchasethings.Purchase_Log_Fail != null)
            {
                purchasethings.Purchase_Log_Fail.SetActive(true);
            }
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
                    UisoundManager?.PlayMouseOverSound();
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
                        UisoundManager?.PlayClick_NextSound();
                    }
                    else if (playNextSound == 1)
                    {
                        UisoundManager?.PlayClick_NoneSound();
                    }
                    else if (playNextSound == 2)
                    {
                        UisoundManager?.PlayCancelSound();
                    }
                    else if (playNextSound == 3)
                    {
                        UisoundManager?.PlaySuccessBtnSound();
                    }
                }
            });
        }
    }

    /*방 생성 시 정보들을 서버로 전송*/
    public void OnButtonSendRoom()
    {
        string roomName = lobbythings.RoomnameText.text;
        string roomPassword = lobbythings.RoompassText.text;
        int roomPlayerNumber = lobbythings.RoompnumText.value;
        string selectedPlayerNumberText = lobbythings.RoompnumText.options[roomPlayerNumber].text;

        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdRoomCre(opponentIdentity, roomName, roomPassword, selectedPlayerNumberText);
        lobbythings.RoomSetUI.SetActive(false);
        lobbythings.Room_Cre_Panl.SetActive(true);
    }
    /*대표 캐릭터 변경*/
    public void OnButtonSendIndex()
    {
        if(charbase != null)
        {
            int index = charbase.Index_Send;
            if (int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out int MainCharacterID))
            {
	    	/*변경 캐릭터가 현재 캐릭터와 동일할 경우 서버로 전송 안함
      		다를 경우 서버로 전송*/
                if(index + 1 == MainCharacterID)
                {
                    charthings.Log_panel.SetActive(true);
                    EventSystem.current.SetSelectedGameObject(charthings.Log_panel_btn.gameObject);
                }
                else
                {
                    NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
                    Change_MainCha(index, opponentIdentity, ClientDataManager.Instance.UserDetails.userID);
                }
            }
        }
    }
    /*방 새로고침 시 서버로 요청해서 방 목록 불러옴*/
    private void ReLoadRooms()
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdReLoadRooms(opponentIdentity);
    }
    /*비밀번호가 없는 방 입장 시 서버로 해당 방 정보를 보내서 입장 가능한지를 확인*/
    private void SendRoom(Room room)
    {
        lobbythings.Room_Enter_Panl.SetActive(true);
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdSendRoom(opponentIdentity, room);
    }
    /*비밀번호가 있는 방 입장 시 서버로 해당 방 정보를 보내서 입장 가능한지를 확인*/
    private void OnSubmitRoomPasswordClicked(Room room)
    {
        string password = lobbythings.Room_Enter_Withpass_InputField.text;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdSubmitRoomPass(opponentIdentity, password, room);
        lobbythings.Room_Enter_Panl.SetActive(true);
    }
    
    /*방에서 강제퇴장 당했을 경우 적용*/
    private IEnumerator WaitForSceneAndActivate()
    {
        while (SceneManager.GetActiveScene().name != "Ingame12")
        {
            yield return null;
        }

        if (lobbythings.Room_Exit_Forced != null)
        {
            lobbythings.Room_Exit_Forced.SetActive(true);
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
    }
    /*메시지 출력 담당*/
    private void HandleNewMessage(string message)
    {
        if (message.StartsWith("\n<color=#FFDA2F>"))
        {
            var receivedWhisperMatch = Regex.Match(message, @"\[\d{2}:\d{2}\] <b>(.*?)<\/b>이 당신에게: ");
            if (receivedWhisperMatch.Success)
            {
	    	/*"/r"로 다시 보내는 닉네임 저장*/
                lastWhisperID = receivedWhisperMatch.Groups[1].Value;
            }
        }
	/*현재 방이면 방으로 메시지 출력*/
        if(chatText == null && RoomchatText != null)
            RoomchatText.text += message;
	/*로비면 로비로 메시지 출력*/
        if(chatText != null)
            chatText.text += message;
	/*로비, 방이 아니면 messageBuffer에 해당 메시지들 최신순으로 30개 저장*/
        else
        {
            messageBuffer.Add(message);
            if (messageBuffer.Count > maxMessageCount)
            {
                messageBuffer.RemoveAt(0);
            }
        }
    }
    /*메시지 30초간 제한, 남은 시간을 출력하기 위해 restrictionTime = 30f을 종료 후로 적용*/
    private IEnumerator RestrictMessaging()
    {
        messageenabled = false;
        while (restrictionTime > 0)
        {
            yield return new WaitForSeconds(1f);
            restrictionTime--;
        }
        restrictionTime = 30f;
        messageenabled = true;
    }

    /*메시지 전송*/
    public void Send(string message)
    {
        if(!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) { return; }
        
        if(string.IsNullOrWhiteSpace(message)) { return; }

 	/*1.메시지제한 : 트래픽 과부하일 경우*/
        if(!messageenabled)
        {
            HandleNewMessage($"\n<color=#FF3B48>메시지 제한! {restrictionTime}초 후 전송 가능</color=#FF3B48>");
            lobbythings.chatInputField.text = string.Empty;
            lobbythings.chatInputField.ActivateInputField();
            return;
        }
	/*2.메시지제한 : 운영자가 직접 벤한 경우*/
        if(ClientDataManager.Instance.ChatBan)
        {
            HandleNewMessage($"\n<color=#FF3B48>                             ----- 메시지 제한! -----</color=#FF3B48>");
            lobbythings.chatInputField.text = string.Empty;
            lobbythings.chatInputField.ActivateInputField();
            return;
        }

        float currentTime = Time.time;
	/*1초 안에 3번 이상 메시지를 보낼 경우 채팅금지 적용*/
        if (currentTime - lastMessageTime < 1f)
        {
            messageCount++;
            if (messageCount > maxMessagesPerSecond)
            {
                HandleNewMessage($"\n<color=#FF3B48>메시지 전송이 너무 빈번합니다. (30초 제한)</color=#FF3B48>");
                StartCoroutine(RestrictMessaging());
                lobbythings.chatInputField.text = string.Empty;
                lobbythings.chatInputField.ActivateInputField();
                return;
            }
        }
        else
        {
            lastMessageTime = currentTime;
            messageCount = 1;
        }

        string trimmedMessage = message.Trim();
        bool isWhisper = trimmedMessage.StartsWith("/w ") || trimmedMessage.StartsWith("/Whisper ")
        || trimmedMessage.StartsWith("/귓 ") || trimmedMessage.StartsWith("/msg ");

	/*채팅에서 비속어는 "#"으로 가려서 출력*/
        foreach (var profanity in profanitiesList)
        {
            if (message.Contains(profanity, StringComparison.OrdinalIgnoreCase))
            {
                string replacement = new string('#', profanity.Length);
                message = Regex.Replace(message, profanity, replacement, RegexOptions.IgnoreCase);
            }
        }

        if (isWhisper)
        {
	    /*귓속말인 경우 "/msg park 메시지"이면 Target은 park, message는 메시지로 Split 후 서버로 전송*/
            string[] splitMessage = trimmedMessage.Split(new char[] { ' ' }, 3);
            if (splitMessage.Length >= 3)
            {
                string whisperTarget = splitMessage[1];
                string whisperMessage = splitMessage[2];
                CmdSendWhisper(whisperTarget, whisperMessage, ClientDataManager.Instance.UserDetails.Nickname);
            }
        }
        else
        {
	    /*일반 메시지일 경우*/
            CmdSendMessage(message, ClientDataManager.Instance.UserDetails.Nickname);
        }
	/*방향키 위를 눌렀으 때 이전 메시지를 다시 입력해주는 기능 최대 3개를 저장*/
        if (messageHistory.Count >= 3)
        {
            messageHistory.RemoveAt(0);
        }
        messageHistory.Add(message);
        currentHistoryIndex = messageHistory.Count;

	/*기존 채팅이 남아있는 것과 채팅을 한번 치고 나면 필드를 벗어나는걸 방지*/
        lobbythings.chatInputField.text = string.Empty;
        lobbythings.chatInputField.ActivateInputField();
    }
    /*방에서의 채팅 전송(방에서의 메시지가 다른 방과 로비에서 적용안되게 하기 위해 분리) - 통합 필요*/
    private void RoomChatSend(string message)
    {
        if(!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) { return; }
        
        if(string.IsNullOrWhiteSpace(message)) { return; }

        if(!messageenabled)
        {
            HandleNewMessage($"\n<color=#FF3B48>메시지 제한! {restrictionTime}초 후 전송 가능</color=#FF3B48>");
            roomthings.RoomchatInputField.text = string.Empty;
            roomthings.RoomchatInputField.ActivateInputField();
            return;
        }
        if(ClientDataManager.Instance.ChatBan)
        {
            HandleNewMessage($"\n<color=#FF3B48>                             ----- 메시지 제한! -----</color=#FF3B48>");
            roomthings.RoomchatInputField.text = string.Empty;
            roomthings.RoomchatInputField.ActivateInputField();
            return;
        }

        float currentTime = Time.time;
        if (currentTime - lastMessageTime < 1f)
        {
            messageCount++;
            if (messageCount > maxMessagesPerSecond)
            {
                HandleNewMessage($"\n<color=#FF3B48>메시지 전송이 너무 빈번합니다. (30초 제한)</color=#FF3B48>");
                StartCoroutine(RestrictMessaging());
                roomthings.RoomchatInputField.text = string.Empty;
                roomthings.RoomchatInputField.ActivateInputField();
                return;
            }
        }
        else
        {
            lastMessageTime = currentTime;
            messageCount = 1;
        }

        string trimmedMessage = message.Trim();
        bool isWhisper = trimmedMessage.StartsWith("/w ") || trimmedMessage.StartsWith("/Whisper ")
        || trimmedMessage.StartsWith("/귓 ") || trimmedMessage.StartsWith("/msg ");


        foreach (var profanity in profanitiesList)
        {
            if (message.Contains(profanity, StringComparison.OrdinalIgnoreCase))
            {
                string replacement = new string('#', profanity.Length);
                message = Regex.Replace(message, profanity, replacement, RegexOptions.IgnoreCase);
            }
        }
        
        if (isWhisper)
        {
            string[] splitMessage = trimmedMessage.Split(new char[] { ' ' }, 3);
            if (splitMessage.Length >= 3)
            {
                string whisperTarget = splitMessage[1];
                string whisperMessage = splitMessage[2];
                CmdSendWhisper(whisperTarget, whisperMessage, ClientDataManager.Instance.UserDetails.Nickname);
            }
        }
        else
        {
            CmdSendMessage(message, ClientDataManager.Instance.UserDetails.Nickname);
        }
        if (messageHistory.Count >= 3)
        {
            messageHistory.RemoveAt(0);
        }
        messageHistory.Add(message);
        currentHistoryIndex = messageHistory.Count;

        roomthings.RoomchatInputField.text = string.Empty;

        roomthings.RoomchatInputField.ActivateInputField();
    }
    private void LoadProfanities()
    {
        string[] profanities = Resources.Load<TextAsset>("profanities").text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        profanitiesList.AddRange(profanities);
    }
    /*귓속말을 받은 경우 OnMessage를 통해 메시지를 출력*/
    private void ReceiveWhisper(string message)
    {
        string coloredMessage = $"{message}";
        OnMessage?.Invoke($"\n{coloredMessage}");
    }
    /*"/r park "입력 시 "/msg park "으로 필드 값 변경*/
    private void HandleInputFieldChange()
    {
        string text = lobbythings.chatInputField.text;
        if (text.StartsWith("/r ", StringComparison.OrdinalIgnoreCase))
        {
            string[] splitText = text.Split(' ', 2);
            if (splitText.Length > 0)
            {
                if (!string.IsNullOrEmpty(lastWhisperID))
                {
                    lobbythings.chatInputField.text = $"/msg {lastWhisperID} ";
                }
                else
                {
                    lobbythings.chatInputField.text = $"/msg ";
                }
		/*커서를 "/msg " 다음으로 이동하지 않아 MoveTextEnd로 커서를 제일 뒤로 옮기고 false를 이용해 해당 텍스트 선택되는 것을 막음*/
                lobbythings.chatInputField.Select();
                lobbythings.chatInputField.MoveTextEnd(false);
            }
        }
    }
    /*동일한 코드(방에서 사용) - 통합 필요*/
    private void HandleRoomInputFieldChange()
    {
        string text = roomthings.RoomchatInputField.text;
        if (text.StartsWith("/r ", StringComparison.OrdinalIgnoreCase))
        {
            string[] splitText = text.Split(' ', 2);
            if (splitText.Length > 0)
            {
                if (!string.IsNullOrEmpty(lastWhisperID))
                {
                    roomthings.RoomchatInputField.text = $"/msg {lastWhisperID} ";
                }
                else
                {
                    roomthings.RoomchatInputField.text = $"/msg ";
                }
		/*커서를 "/msg " 다음으로 이동하지 않아 MoveTextEnd로 커서를 제일 뒤로 옮기고 false를 이용해 해당 텍스트 선택되는 것을 막음*/
                roomthings.RoomchatInputField.Select();
                roomthings.RoomchatInputField.MoveTextEnd(false);
            }
        }
    }
    /*Char 씬에서 의상 변경 사항을 서버로 전송*/
    private void HandleSendCloth(string info)
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        string[] values = info.Split(';');
        int characterNum = int.Parse(values[0]);
        string type = values[1];
        int outfitID = int.Parse(values[2]);
        string description = values[3];
        Change_ChaCloths(opponentIdentity, characterNum, type, outfitID, ClientDataManager.Instance.UserDetails.userID);
    }

    /*Shop 씬에서 구매 의상을 팝업을 띄워 상세 정보를 확인, 결제를 캐시와 인게임재화 중 어떤 것으로 선택할지 정할 수 있음*/
    private void HandleSendItem(string info)
    {
        string[] values = info.Split(';');
        int characterNum = int.Parse(values[0]);
        string type = values[1];
        string description = values[2];
        string ItemName = values[3];
	/*CP,1000&BP,2000와 같은 값을 ["CP,1000", "BP,2000"]으로 나눈 뒤 다시 분리하여 type과 price로 저장*/
        string[] priceEntries = values[4].Split('&');
        List<PriceData> prices = priceEntries.Select(entry => {
            string[] priceInfo = entry.Split(',');
            return new PriceData { price_Type = priceInfo[0], price = int.Parse(priceInfo[1]) };
        }).ToList();

        shopthings.Shop_textCloth.text = ItemName;
        Sprite itemSprite_Item = Resources.Load<Sprite>($"Images/{description}");
        shopthings.Shop_outfitImage.sprite = itemSprite_Item;

 	/*기본 옵션을 제거한 후 드롭다운에 가격 정보를 기입*/
        shopthings.Shop_dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>();
        foreach (var priceData in prices)
        {
            string optionText = $"{priceData.price:N0}";
            Sprite optionImage = priceData.price_Type == "CP" ? Cpimage : Bpimage;

            TMP_Dropdown.OptionData optionData = new TMP_Dropdown.OptionData(optionText, optionImage);
            dropdownOptions.Add(optionData);
        }
	/*드롭다운 옵션을 적용, 초기 값들 출력*/
        shopthings.Shop_dropdown.AddOptions(dropdownOptions);
        shopthings.Shop_textCost.text = shopthings.Shop_dropdown.options[Shop_dropdown.value].text;
        shopthings.Shop_CostImage.sprite = shopthings.Shop_dropdown.options[Shop_dropdown.value].image;
        shopthings.Shop_toggle.isOn = true;
	/*버튼 클릭 시 해당 값으로 서버에 전송할 수 있게 값을 저장*/
        UpdateButtonBuyValues(characterNum, type, 
        description, ItemName, shopthings.Shop_textCost.text, shopthings.Shop_CostImage.sprite.name, shopthings.Shop_toggle.isOn);
        buy_MainPanel.SetActive(true);
    }
    /*드롭다운으로 값을 변경했을 때 팝업에서 출력값 변경 및 값 저장*/
    private void DropdownValueChanged(TMP_Dropdown dropdown)
    {
        string selectedOptionText = dropdown.options[dropdown.value].text;
        shopthings.Shop_textCost.text = selectedOptionText;
        shopthings.Shop_CostImage.sprite = dropdown.options[dropdown.value].image;
        UpdateButtonBuyValues(Shop_characterNum, Shop_type, 
        Shop_description, Shop_itemName, shopthings.Shop_textCost.text, shopthings.Shop_CostImage.sprite.name, Shop_isWorn);
    }
    /*구매 후 바로 입을지를 위한 Toggle값 변경*/
    private void ToggleValueChanged(bool isOn)
    {
        Shop_isWorn = isOn;
        UpdateButtonBuyValues(Shop_characterNum, Shop_type, Shop_description, Shop_itemName, Shop_price, Shop_priceType, Shop_isWorn);
    }
    /*구매할 아이템의 값을 저장*/
    private void UpdateButtonBuyValues(int chaNum, string t, string desc, string iName, string p, string pType, bool worn)
    {
        Shop_characterNum = chaNum;
        Shop_type = t;
        Shop_description = desc;
        Shop_itemName = iName;
	/*표기형식이 N0인 것을 파싱하여 저장*/
        if (int.TryParse(p, System.Globalization.NumberStyles.AllowThousands | System.Globalization.NumberStyles.Integer, null, out int shopPrice))
        {
            Shop_price = shopPrice.ToString();
        }
        Shop_priceType = pType;
        Shop_isWorn = worn;
    }
    /*구매 확정 시 서버로 저장된 값들 전송*/
    void OnButtonItemBuy(int ChaNum, string Type, string Description, string ItemName, string Price, string Price_Type, bool isWorn)
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        shopthings.buyCheckLast_Panel.SetActive(false);
        shopthings.buy_MainPanel.SetActive(false);
        BuyButton_Check(opponentIdentity, ChaNum, Type, Description, ItemName, Price, Price_Type, isWorn, ClientDataManager.Instance.UserDetails.userID);
    }
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /*씬에 따라 Initialize 실행*/
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name == "Ingame12")
        {
            Initialize();
        }
        else if(scene.name == "Char_Scenes")
        {
            Initialize_Char();
        }
        else if(scene.name == "Cus_Scenes")
        {
            Initialize_Cus();
        }
        else if(scene.name == "Shop_Scenes")
        {
            Initialize_Shop();
        }
        
        else if(scene.name == "Ingame")
        {
            Initialize_Room();
        }
        
    }

    void Update()
    {
        if (!isClient) return;
        /*채팅 인풋필드가 선택되어 있고 위 방향키를 누를 때 전에 전송한 메시지 인풋필드에 입력*/
        if(roomthings.RoomchatInputField != null && roomthings.RoomchatInputField.isFocused && Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (messageHistory.Count > 0)
            {
                currentHistoryIndex--;
                if (currentHistoryIndex < 0)
                {
                    currentHistoryIndex = messageHistory.Count - 1;
                }
                roomthings.RoomchatInputField.text = messageHistory[currentHistoryIndex];
		/*이렇게 입력값을 변경했을 경우 커서가 첫번째 칸에 머물러 있어서 마지막 위치로 이동시킴*/
                roomthings.RoomchatInputField.caretPosition = roomthings.RoomchatInputField.text.Length;
            }
        }
        else if(lobbythings.chatInputField != null && lobbythings.chatInputField.isFocused && Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (messageHistory.Count > 0)
            {
                currentHistoryIndex--;
                if (currentHistoryIndex < 0)
                {
                    currentHistoryIndex = messageHistory.Count - 1;
                }
                lobbythings.chatInputField.text = messageHistory[currentHistoryIndex];
                lobbythings.chatInputField.caretPosition = lobbythings.chatInputField.text.Length;
            }
        }

    }
    
}
