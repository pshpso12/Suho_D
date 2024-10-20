using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[System.Serializable]
public class Item
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
    public bool Ready;
}
[System.Serializable]
public class CharacterCostumeInfo
{
    public int CharacterID;
    public string TopDescription;
    public string BottomDescription;
    public string ShoesDescription;
}
public class RoomData_Client : NetworkBehaviour
{
    [SyncVar(hook = nameof(SetPlayerIndex))]
    public int PlayerIndex;
    [SyncVar(hook = nameof(SetRoomNumber))]
    public int CurrnetRoomNumber;
    [SyncVar(hook = nameof(SetRoomName))]
    public string CurrnetRoomName;
    [SyncVar]
    public int CurrnetMaxRoomPNumber;
    [SyncVar]
    public int CurrnetCurrentRoomPNumber;
    [SyncVar(hook = nameof(SetRoomPassword))]
    public string CurrnetPassword;
    [SyncVar]
    public Guid RoomId;

    private bool canOpenContextMenu = true;
    private bool isRoomInitialized = false;

    public readonly SyncList<Item> inventory = new SyncList<Item>();

    private RoomBase roombase;
    private Room_Things roomthings;
    public GameObject PlayerObj;
    [SerializeField] private GameObject myNetworkGameRoomPrefab;
    [SerializeField] private GameObject CloneT;
    
    [SyncVar]
    public bool isDummy;
    [SyncVar]
    public bool GetRoomData;
    [SyncVar]
    public bool GetCharacters;
    [SyncVar]
    public bool GetGameScene;
    [SyncVar]
    public bool RealSceneChnage;
    [SyncVar]
    public bool ChangeProgress;
    
    public string NetUniqueID;
    public GameObject CloneT_G;
    public Clone_Datas CDatas;

    public UISoundManager UisoundManager;

    void Start()
    {
        if(isClient)
        {
            DontDestroyOnLoad(this);
            inventory.Callback += OnInventoryUpdated;
        }
    }

    public void Initialize_Room()
    {
        if(isClient && isOwned)
        {
            /*게임서버에서 방으로 입장 시 게임 데이터 삭제*/
            Clone_Datas CloneDatas_22 = FindObjectOfType<Clone_Datas>();
            if(CloneDatas_22 != null)
                Destroy(CloneDatas_22.gameObject);
                
            roomthings.PReadybtn.onClick.AddListener(OnButtonPlayerReady);
            roomthings.HReadybtn.onClick.AddListener(OnButtonHostStart);
            roomthings.Host_RoomReadyBtn_T.SetActive(false);

            roomthings.RoomPassBtn_T.onClick.AddListener(OnButtonChangeRoomPass);
            roomthings.RoomTitle_T.onEndEdit.AddListener(OnRoomTitleChanged);
            roomthings.RoomExitBtn_T.onClick.AddListener(OnButtonRoomExit);
            
            GameObject SceneManGameObject = GameObject.Find("SceneManger");
            if(SceneManGameObject != null)
                roombase = SceneManGameObject.GetComponent<RoomBase>();

            UisoundManager = GameObject.Find("UI_SoundObject").GetComponent<UISoundManager>();

            AddButtonListeners(roomthings.RoomExitBtn_T, true, true, 0);
            AddButtonListeners(roomthings.RoomPassBtn_T, true, true, 0);
            roomthings.RoomTitle_T.onEndEdit.AddListener((string text) => {
                if(inventory[PlayerIndex].FakeHost == true && text != CurrnetRoomName)
                {
                    UisoundManager?.PlayClick_NextSound();
                }
            });
            AddReadyButtonListeners(roomthings.PReadybtn, true, true);
            AddButtonListeners(roomthings.HReadybtn, true, false, 0);
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
        if(scene.name == " Ingame" && isOwned)
        {
            Initialize_Room();
            isRoomInitialized = true;

            /*현재 방의 정보 불러오기*/
            SetRoomNumber(CurrnetRoomNumber, CurrnetRoomNumber);
            SetRoomName(CurrnetRoomName, CurrnetRoomName);
            SetRoomPassword(CurrnetPassword, CurrnetPassword);
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            /*나의 정보를 서버로 전송*/
            SetRoomPlayer_R1();
            /*방의 유저 정보를 동기화*/
            OriginalPlayer_R1(opponentIdentity, CurrnetRoomNumber);
        }
    }
    /*씬 변경이 완료된 경우, 방 번호를 입력*/
    void SetRoomNumber(int oldNumber, int newNumber)
    {
        if(isRoomInitialized)
        {
            roomthings.RoomNumber_T.text = newNumber.ToString();
        }
    }
    /*씬 변경이 완료된 경우, 방 제목을 입력*/
    void SetRoomName(string oldName, string newName)
    {
        if(isRoomInitialized)
        {
            roomthings.RoomTitle_T.text = newName;
        }
    }
    /*씬 변경이 완료된 경우, 방 비밀번호를 입력*/
    void SetRoomPassword(string oldPass, string newPass)
    {
        if(isRoomInitialized)
        {
            roomthings.RoomPass_T.text = newPass;
        }
    }
    /*방 비밀번호 바꾸기*/
    void OnButtonChangeRoomPass()
    {
        /*방의 방장이며 현재 비밀번호와 다를 경우 변경*/
        if(inventory[PlayerIndex].FakeHost == true && roomthings.RoomPass_T.text != CurrnetPassword)
        {
            string roomPassword = roomthings.RoomPass_T.text;
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            /*서버로 방의 정보와 변경할 비밀번호를 전송*/
            CmdChangeRoomPass(opponentIdentity, roomPassword, CurrnetRoomNumber, RoomId);
        }
    }
    /*방 제목 변경*/
    void OnRoomTitleChanged(string newTitle)
    {
        /*방의 방장이며 현재 제목과 다를 경우 변경*/
        if(inventory[PlayerIndex].FakeHost == true && newTitle != CurrnetRoomName)
        {
            /*방 생성 시와 동일하게 비속어와 제목이 없을 경우를 검사하기 위함*/
            string CheknewTitle = RoomManager.Instance.CheckRoomName(newTitle);
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            /*서버로 방의 정보와 변경할 제목을 전송*/
            CmdChangeRoomTitle(opponentIdentity, CheknewTitle, CurrnetRoomNumber, RoomId);
        }
    }
    /*방 나가기 버튼*/
    void OnButtonRoomExit()
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*서버로 방 나감을 전송*/
        CmdPlayerWantsToLeaveRoom(opponentIdentity, CurrnetRoomNumber);
    }
    /*유저 준비 버튼*/
    void OnButtonPlayerReady()
    {
        roomthings.PReadybtn.interactable = false;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*유저 준비 변경을 서버로 전송*/
        CmdChangeRoomReday(opponentIdentity, CurrnetRoomNumber);
        /*지속적인 준비 변경을 막기 위해 0.5초간 버튼 클릭 비활성화*/
        StartCoroutine(EnableButtonsAfterDelay(0.5f));
    }
    IEnumerator EnableButtonsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        roomthings.PReadybtn.interactable = true;
    }
    /*방장 게임시작 버튼*/
    void OnButtonHostStart()
    {
        roomthings.HReadybtn.interactable = false;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*게임시작을 서버에 전송*/
        CmdHostStart(opponentIdentity, CurrnetRoomNumber);
        /*연속적인 게임시작 버튼 클릭 방지*/
        StartCoroutine(EnableButtonsAfterDelay_H(5f));
        /*RoomId로 새로운 게임 정보를 서버로 보내며, 서버에서 새로운 게임서버 프로세스 생성이 가능합니다.*/
        var clientMatchMaking = FindObjectOfType<Insight.ClientMatchMaking>();
        if (clientMatchMaking != null)
        {
            var startMatchMakingMsg = new Insight.StartMatchMakingMsg
            {
                SceneName = RoomId.ToString()
            };
            clientMatchMaking.SendStartMatchMaking(startMatchMakingMsg);
        }
    }
    IEnumerator EnableButtonsAfterDelay_H(float delay)
    {
        yield return new WaitForSeconds(delay);
        roomthings.HReadybtn.interactable = true;
    }
    /*방장이 게임 시작 시 인원들이 방을 나가거나 준비해제, 자리이동, 강제퇴장 방지하기 위함*/
    IEnumerator EnableButtonsAfterDelay_Lock(float delay)
    {
        yield return new WaitForSeconds(delay);
        roomthings.RoomExitBtn_T.interactable = true;
        roomthings.PReadybtn.interactable = true;
        canOpenContextMenu = true;
    }
    /*방에서 게임으로 로딩씬*/
    IEnumerator Count_DownText()
    {
        yield return new WaitForSeconds(0.01f);

        /*로딩 이미지를 4개 중 하나 무작위로 표시 로딩창의 투명도를 0에서 255로 변경하여 페이드인효과 추가*/
        int randomIndex = UnityEngine.Random.Range(0, roombase.GloadList.Length);
        Image panelImage = roomthings.GLoadPanel.GetComponent<Image>();
        panelImage.sprite = roombase.GloadList[randomIndex];
        Color panelColor = panelImage.color;
        panelColor.a = 0;
        panelImage.color = panelColor;

        /*로딩바도 동일하게 페이드인 효과*/
        Image sliderBackgroundImage = roomthings.GLoadSlider.transform.Find("Background").GetComponent<Image>();
        Color sliderBgColor = sliderBackgroundImage.color;
        sliderBgColor.a = 0;
        sliderBackgroundImage.color = sliderBgColor;

        /*로딩텍스트도 동일하게 페이드인 효과*/
        Color textVertexColor = roomthings.GLoadingSlider_Text.color;
        textVertexColor.a = 0;
        roomthings.GLoadingSlider_Text.color = textVertexColor;

        roomthings.GLoadPanel.SetActive(true);

        float duration = 0.6f;
        float currentTime = 0f;
        /*페이드인 효과*/
        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, currentTime / duration);

            panelColor.a = alpha;
            panelImage.color = panelColor;

            sliderBgColor.a = alpha;
            sliderBackgroundImage.color = sliderBgColor;

            textVertexColor.a = alpha;
            roomthings.GLoadingSlider_Text.color = textVertexColor;
            yield return null;
        }
        
        panelColor.a = 1;
        panelImage.color = panelColor;
        sliderBgColor.a = 1;
        sliderBackgroundImage.color = sliderBgColor;
        textVertexColor.a = 1;
        roomthings.GLoadingSlider_Text.color = textVertexColor;
        CreRoomDummy(randomIndex);
    }
    /*마스터 서버에서 게임서버로 이동 시 현재 데이터를 저장하기 위함*/
    private void CreRoomDummy(int ImageIndex)
    {
        if(CloneT_G == null)
        {
            CloneT_G = Instantiate(CloneT);
        }
        CDatas = CloneT_G.GetComponent<Clone_Datas>();
        CDatas.PlayerIndex = this.PlayerIndex;
        CDatas.CurrnetRoomNumber = this.CurrnetRoomNumber;
        CDatas.RoomId = this.RoomId;
        CDatas.ImageIndex = ImageIndex;
        CDatas.Gameinventory.Clear();
        foreach(Item Item in inventory)
        {
            GameItem newItem = new GameItem()
            {
                PlayerExist = Item.PlayerExist,
                FakeHost = Item.FakeHost,
                PlayerNickname = Item.PlayerNickname,
                PlayerLevel = Item.PlayerLevel,
                PlayerTexture = Item.PlayerTexture,
                PlayerMainCharacterID = Item.PlayerMainCharacterID,
                PlayerTopOutfitID = Item.PlayerTopOutfitID,
                PlayerBottomOutfitID = Item.PlayerBottomOutfitID,
                PlayerShoesOutfitID = Item.PlayerShoesOutfitID,
                PlayerAllInOneOutfitID = Item.PlayerAllInOneOutfitID,
                PConnection = true
            };
            CDatas.Gameinventory.Add(newItem);
        }
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.15f, 0.5f));
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdCGetRoomData(opponentIdentity);
    }
    private string GetOutfitDescription(int outfitID)
    {
        var outfit = ClientDataManager.Instance.OutfitData.outfits
            .FirstOrDefault(o => o.OutfitID == outfitID);
        return outfit != null ? outfit.Description : null;
    }
    /*로딩바 기존값에서 추가 로딩*/
    private IEnumerator IncreaseSliderOverTime(float targetValue, float duration)
    {
        float startValue = GLoadSlider.value;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            roomthings.GLoadSlider.value = Mathf.Lerp(startValue, targetValue, time / duration);
            roomthings.GLoadingSlider_Text.text = Mathf.RoundToInt(roomthings.GLoadSlider.value * 100).ToString() + "%";
            yield return null;
        }

        roomthings.GLoadSlider.value = targetValue;
        roomthings.GLoadingSlider_Text.text = Mathf.RoundToInt(targetValue * 100).ToString() + "%";
    }

    /*레벨, 닉네임, 이미지 등과 메인캐릭터의 번호와 의상 정보를 서버로 보냄*/
    void SetRoomPlayer_R1()
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        string SendNickname = ClientDataManager.Instance.UserDetails.Nickname;
        int SendLevel = ClientDataManager.Instance.UserDetails.Level;
        Texture2D SendTexture = ClientDataManager.Instance.UserTexture;
        byte[] textureBytes = (SendTexture != null) ? SendTexture.EncodeToJPG() : null;
        string SendMainCha = ClientDataManager.Instance.UserDetails.MainCharacterID;

        string sendTop = null, sendBottom = null, sendShoes = null;
        if(int.TryParse(SendMainCha, out int Sendchain))
        {
            var Maincha = ClientDataManager.Instance.CharacterData.characters
                .FirstOrDefault(character => character.CharacterType == Sendchain);
            if(Maincha != null)
            {
                if (int.TryParse(Maincha.TopOutfitID, out int outTop))
                {
                    var outfitTop = ClientDataManager.Instance.OutfitData.outfits
                        .FirstOrDefault(o => o.OutfitID == outTop);
                    if (outfitTop != null) sendTop = outfitTop.Description;
                }
                if (int.TryParse(Maincha.BottomOutfitID, out int outBottom))
                {
                    var outfitBottom = ClientDataManager.Instance.OutfitData.outfits
                        .FirstOrDefault(o => o.OutfitID == outBottom);
                    if (outfitBottom != null) sendBottom = outfitBottom.Description;
                }
                if (int.TryParse(Maincha.ShoesOutfitID, out int outShoes))
                {
                    var outfitShoes = ClientDataManager.Instance.OutfitData.outfits
                        .FirstOrDefault(o => o.OutfitID == outShoes);
                    if (outfitShoes != null) sendShoes = outfitShoes.Description;
                }
            }
        }
        CmdSetRoomPlayer(
            opponentIdentity,
            PlayerIndex,
            inventory[PlayerIndex].FakeHost,
            SendNickname,
            SendLevel,
            textureBytes,
            SendMainCha,
            sendTop,
            sendBottom,
            sendShoes
        );
    }

    /*방에서 나가거나, 자리변경 등 서버에서 방의 Item이 변경된 경우 클라이언트에서 진행함*/
    void ITem_OPSET(int index, Item oldItem, Item newItem)
    {
        /*유저가 방에 들어오거나, 자리를 변경한 경우*/
        if(oldItem.PlayerExist == false && newItem.PlayerExist == true)
        {
            GameObject RefCha = roomthings.playerUIElements[index].Stand_Player;
            GameObject RefUi = roomthings.playerUIElements[index].Canvas_Player;
            if(int.TryParse(newItem.PlayerMainCharacterID, out int RefChaId))
            {
                /*캐릭터를 위치에 맞게 생성 및 의상을 입힘*/
                GameObject prefab = roombase.characterPrefabs[RefChaId -1];
                roomthings.playerUIElements[index].currentCharacter = Instantiate(prefab, RefCha.transform.position, RefCha.transform.rotation);
                roomthings.playerUIElements[index].currentCharacter.transform.localScale = RefCha.transform.localScale;
                
                Transform categoryTop = roomthings.playerUIElements[index].currentCharacter.transform.Find("Top");
                SetOutfit(categoryTop, newItem.PlayerTopOutfitID);

                Transform categoryBottom = roomthings.playerUIElements[index].currentCharacter.transform.Find("Bottom");
                SetOutfit(categoryBottom, newItem.PlayerBottomOutfitID);

                Transform categoryShoes = roomthings.playerUIElements[index].currentCharacter.transform.Find("Shoes");
                SetOutfit(categoryShoes, newItem.PlayerShoesOutfitID);
            }
            /*닉네임과 레벨, 이미지를 기입*/
            TMP_Text nickText = RefUi.transform.Find("Nick_text").GetComponent<TMP_Text>();
            TMP_Text levelText = RefUi.transform.Find("Level_text").GetComponent<TMP_Text>();
            Image imageStream = RefUi.transform.Find("Image_mask/Image_steam").GetComponent<Image>();
                
            nickText.text = newItem.PlayerNickname;
            levelText.text = $"LV. {newItem.PlayerLevel}";
            if (newItem.PlayerTexture != null && newItem.PlayerTexture.Length > 0)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(newItem.PlayerTexture);
                imageStream.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            /*해당 위치의 UI요소 활성화, 초기 레디 표시는 전부 false*/
            roomthings.playerUIElements[index].Canvas_Player.SetActive(true);
            roomthings.playerUIElements[index].CanP_Host.SetActive(newItem.FakeHost);
            roomthings.playerUIElements[index].CanP_Ready.SetActive(newItem.Ready);
            if(index == PlayerIndex)
            {
                /*해당 유저가 방장이면 비밀번호, 제목 변경을 활성화*/
                if(newItem.FakeHost == true)
                {
                    roomthings.RoomPassBtn_T.gameObject.SetActive(true);
                    roomthings.RoomTitle_T.interactable = true;
                    roomthings.RoomPass_T.interactable = true;
                }
                else if(newItem.FakeHost == false)
                {
                    roomthings.RoomPassBtn_T.gameObject.SetActive(false);
                    roomthings.RoomTitle_T.interactable = false;
                    roomthings.RoomPass_T.interactable = false;
                }
            }
        }
        /*방장 위임과 준비 변경*/
        else if(oldItem.PlayerExist == true && newItem.PlayerExist == true)
        {
            /*방장 위임*/
            if(oldItem.FakeHost != newItem.FakeHost)
            {
                roomthings.playerUIElements[index].CanP_Host.SetActive(newItem.FakeHost);
                if(index == PlayerIndex)
                {
                    if(newItem.FakeHost == true)
                    {
                        roomthings.RoomPassBtn_T.gameObject.SetActive(true);
                        roomthings.RoomTitle_T.interactable = true;
                        roomthings.RoomPass_T.interactable = true;
                    }
                    else if(newItem.FakeHost == false)
                    {
                        roomthings.RoomPassBtn_T.gameObject.SetActive(false);
                        roomthings.RoomTitle_T.interactable = false;
                        roomthings.RoomPass_T.interactable = false;
                    }
                }
            }
            /*준비 변경*/
            if(oldItem.Ready != newItem.Ready)
            {
                roomthings.playerUIElements[index].CanP_Ready.SetActive(newItem.Ready);
            }
            /*로비에서 방으로 이동, 예외사항을 위함*/
            if(oldItem.FakeHost == newItem.FakeHost && oldItem.Ready == newItem.Ready && oldItem.PlayerNickname == newItem.PlayerNickname)
            {
                GameObject RefCha = roomthings.playerUIElements[index].Stand_Player;
                GameObject RefUi = roomthings.playerUIElements[index].Canvas_Player;
                if(int.TryParse(newItem.PlayerMainCharacterID, out int RefChaId))
                {
                    GameObject prefab = roombase.characterPrefabs[RefChaId -1];
                    roomthings.playerUIElements[index].currentCharacter = Instantiate(prefab, RefCha.transform.position, RefCha.transform.rotation);

                    roomthings.playerUIElements[index].currentCharacter.transform.localScale = RefCha.transform.localScale;
                    
                    Transform categoryTop = roomthings.playerUIElements[index].currentCharacter.transform.Find("Top");
                    SetOutfit(categoryTop, newItem.PlayerTopOutfitID);

                    Transform categoryBottom = roomthings.playerUIElements[index].currentCharacter.transform.Find("Bottom");
                    SetOutfit(categoryBottom, newItem.PlayerBottomOutfitID);

                    Transform categoryShoes = roomthings.playerUIElements[index].currentCharacter.transform.Find("Shoes");
                    SetOutfit(categoryShoes, newItem.PlayerShoesOutfitID);
                }
                    
                TMP_Text nickText = RefUi.transform.Find("Nick_text").GetComponent<TMP_Text>();
                TMP_Text levelText = RefUi.transform.Find("Level_text").GetComponent<TMP_Text>();
                Image imageStream = RefUi.transform.Find("Image_mask/Image_steam").GetComponent<Image>();
                    
                nickText.text = newItem.PlayerNickname;
                levelText.text = $"LV. {newItem.PlayerLevel}";
                if (newItem.PlayerTexture != null && newItem.PlayerTexture.Length > 0)
                {
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(newItem.PlayerTexture);
                    imageStream.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
                roomthings.playerUIElements[index].Canvas_Player.SetActive(true);
                roomthings.playerUIElements[index].CanP_Host.SetActive(newItem.FakeHost);
                roomthings.playerUIElements[index].CanP_Ready.SetActive(newItem.Ready);
                if(index == PlayerIndex)
                {
                    if(newItem.FakeHost == true)
                    {
                        roomthings.RoomPassBtn_T.gameObject.SetActive(true);
                        roomthings.RoomTitle_T.interactable = true;
                        roomthings.RoomPass_T.interactable = true;
                    }
                    else if(newItem.FakeHost == false)
                    {
                        roomthings.RoomPassBtn_T.gameObject.SetActive(false);
                        roomthings.RoomTitle_T.interactable = false;
                        roomthings.RoomPass_T.interactable = false;
                    }
                }
            }
        }
        /*방에서 나갈 경우*/
        else if(oldItem.PlayerExist == true && newItem.PlayerExist == false)
        {
            GameObject RefCha = roomthings.playerUIElements[index].Stand_Player;
            GameObject RefUi = roomthings.playerUIElements[index].Canvas_Player;
            if (roomthings.playerUIElements[index].currentCharacter != null)
            {
                Destroy(roomthings.playerUIElements[index].currentCharacter);
            }
            TMP_Text nickText = RefUi.transform.Find("Nick_text").GetComponent<TMP_Text>();
            TMP_Text levelText = RefUi.transform.Find("Level_text").GetComponent<TMP_Text>();
            
            nickText.text = "";
            levelText.text = $"LV. {0}";
            roomthings.playerUIElements[index].Canvas_Player.SetActive(false);
            roomthings.playerUIElements[index].CanP_Host.SetActive(false);
            roomthings.playerUIElements[index].CanP_Ready.SetActive(false);
        }
    }
    /*item이 변경 될 때 마다 준비를 확인, 방장일 경우 존재하는 모든 유저가 준비 완료일 때 게임시작 버튼 활성화*/
    void UpdateGameStatus()
    {
        bool allReady = true;
        foreach (var item in inventory)
        {
            if (item.PlayerExist && !item.Ready)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            if(inventory[PlayerIndex].FakeHost)
            {
                roomthings.Host_RoomReadyBtn_T.SetActive(true);
                roomthings.RoomReadyBtn_T.SetActive(false);
            }
            else if(!inventory[PlayerIndex].FakeHost)
            {
                roomthings.Host_RoomReadyBtn_T.SetActive(false);
                roomthings.RoomReadyBtn_T.SetActive(true);
            }
        }
        else
        {
            if(inventory[PlayerIndex].FakeHost)
            {
                roomthings.Host_RoomReadyBtn_T.SetActive(false);
                roomthings.RoomReadyBtn_T.SetActive(true);
            }
            else if(!inventory[PlayerIndex].FakeHost)
            {
                roomthings.Host_RoomReadyBtn_T.SetActive(false);
                roomthings.RoomReadyBtn_T.SetActive(true);
            }
        }
    }
    /*캐릭터 의상 적용*/
    private void SetOutfit(Transform parentTransform, string outfitID)
    {
        if (parentTransform != null)
        {
            foreach (Transform child in parentTransform)
            {
                bool shouldEnable = child.name == outfitID;
                child.gameObject.SetActive(shouldEnable);
            }
        }
    }
    /*Synclist인 item이 변경되면 클라이언트에서 실행*/
    void OnInventoryUpdated(SyncList<Item>.Operation op, int index, Item oldItem, Item newItem)
    {
        switch (op)
        {
            case SyncList<Item>.Operation.OP_SET:
                ITem_OPSET(index, oldItem, newItem);
                UpdateGameStatus();
                break;
        }
    }
    /*마우스 우클릭 시 강제퇴장, 방장위임, 자리변경이 가능한 메뉴바 생성, ESC와 좌클릭 시 메뉴바 삭제*/
    void Update()
    {
        if (!isClient || !isOwned) return;

        if (Input.GetMouseButtonDown(1))
        {
            CheckMousePosition();
        }
        if (currentContextMenu != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseContextMenu();
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsClickOnContextMenu())
                {
                    CloseContextMenu();
                }
            }
        }
    }
    
    void CheckMousePosition()
    {
        /*게임을 시작해서 canOpenContextMenu가 false인 경우 return*/
        if (!canOpenContextMenu) return;
        
        GraphicRaycaster graphicRaycaster = roomthings.canvase.GetComponent<GraphicRaycaster>();

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEventData, results);

        /*마우스 위치로 인덱스를 확인하기 때문에 해당 범위에 우클릭 시에 메뉴바를 생성*/
        foreach (RaycastResult result in results)
        {
            for (int i = 0; i < roomthings.playerUIElements.Count(); i++)
            {
                if (result.gameObject == roomthings.playerUIElements[i].Index_Area)
                {

                    OpenContextMenu(Input.mousePosition, i);
                    return;
                }
            }
        }
    }
    /*메뉴바 생성*/
    void OpenContextMenu(Vector3 position, int index)
    {
        /*메뉴바가 이미 있으면 우선 삭제*/
        if (roomthings.currentContextMenu != null)
        {
            CloseContextMenu();
        }

        roomthings.currentContextMenu = Instantiate(roomthings.contextMenuPrefab, roomthings.canvase.transform, false);
        roomthings.currentContextMenu.SetActive(true);

        /*위치 변경은 누구나 가능하지만, 방장위임과 강제퇴장은 방장만 가능하고 해당 위치에 유저가 있어야 가능*/
        roomthings.ContextMenuOption1 = roomthings.currentContextMenu.transform.Find("C_Index").gameObject;
        roomthings.ContextMenuOption2 = roomthings.currentContextMenu.transform.Find("C_Host").gameObject;
        roomthings.ContextMenuOption3 = roomthings.currentContextMenu.transform.Find("E_User").gameObject;
        bool Option1Checker = !inventory[index].PlayerExist && index != PlayerIndex;
        bool Option2Checker = inventory[PlayerIndex].FakeHost == true && inventory[index].PlayerExist && index != PlayerIndex;
        bool Option3Checker = inventory[PlayerIndex].FakeHost == true && inventory[index].PlayerExist && index != PlayerIndex;
        SetOptionActive(1, Option1Checker, index);
        SetOptionActive(2, Option2Checker, index);
        SetOptionActive(3, Option3Checker, index);

        /*위치 조정*/
        RectTransformUtility.ScreenPointToLocalPointInRectangle(roomthings.canvase.GetComponent<RectTransform>(), position, null, out Vector2 localPoint);
        currentContextMenu.GetComponent<RectTransform>().localPosition = localPoint;
        if(UisoundManager != null)
        {
            UisoundManager.PlayClick_NextSound();
            AddButtonListeners(roomthings.ContextMenuOption1.GetComponent<Button>(), true, true, 0);
            AddButtonListeners(roomthings.ContextMenuOption2.GetComponent<Button>(), true, true, 0);
            AddButtonListeners(roomthings.ContextMenuOption3.GetComponent<Button>(), true, true, 0);
        }
    }
    /*버튼에 따른 onClick 추가*/
    public void SetOptionActive(int optionNumber, bool active, int index)
    {
        switch (optionNumber)
        {
            case 1:
                if (roomthings.ContextMenuOption1 != null)
                {
                    roomthings.ContextMenuOption1.SetActive(active);
                    if (active)
                    {
                        Button btnOption1 = roomthings.ContextMenuOption1.GetComponent<Button>();
                        btnOption1.onClick.RemoveAllListeners();
                        btnOption1.onClick.AddListener(() => OnClickContextMenuOption1(index));
                    }
                }
                break;
            case 2:
                if (roomthings.ContextMenuOption2 != null)
                {
                    roomthings.ContextMenuOption2.SetActive(active);
                    if (active)
                    {
                        Button btnOption2 = roomthings.ContextMenuOption2.GetComponent<Button>();
                        btnOption2.onClick.RemoveAllListeners();
                        btnOption2.onClick.AddListener(() => OnClickContextMenuOption2(index));
                    }
                }
                break;
            case 3:
                if (roomthings.ContextMenuOption3 != null)
                {
                    roomthings.ContextMenuOption3.SetActive(active);
                    if (active)
                    {
                        Button btnOption3 = roomthings.ContextMenuOption3.GetComponent<Button>();
                        btnOption3.onClick.RemoveAllListeners();
                        btnOption3.onClick.AddListener(() => OnClickContextMenuOption3(index));
                    }
                }
                break;
        }
    }
    /*Updatae()에서 좌클릭 시 메뉴를 닫게 하는데 메뉴바 클릭은 가능하게 하기 위함*/
    bool IsClickOnContextMenu()
    {
        GraphicRaycaster uiRaycaster = roomthings.canvase.GetComponent<GraphicRaycaster>();
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        uiRaycaster.Raycast(pointerEventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == roomthings.currentContextMenu.transform.gameObject)
            {
                return true;
            }
        }
        return false;
    }
    /*메뉴바 삭제*/
    void CloseContextMenu()
    {
        if (roomthings.currentContextMenu != null)
        {
            Destroy(roomthings.currentContextMenu);
        }
    }
    /*버튼 별 기능*/
    void OnClickContextMenuOption1(int index)
    {
        if (roomthings.currentContextMenu != null)
        {
            Destroy(roomthings.currentContextMenu);
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdMaunOption1(opponentIdentity, CurrnetRoomNumber, index);
    }
    void OnClickContextMenuOption2(int index)
    {
        if (roomthings.currentContextMenu != null)
        {
            Destroy(roomthings.currentContextMenu);
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdMaunOption2(opponentIdentity, CurrnetRoomNumber, index);
    }
    void OnClickContextMenuOption3(int index)
    {
        if (roomthings.currentContextMenu != null)
        {
            Destroy(roomthings.currentContextMenu);
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdMaunOption3(opponentIdentity, CurrnetRoomNumber, index);
    }
}

