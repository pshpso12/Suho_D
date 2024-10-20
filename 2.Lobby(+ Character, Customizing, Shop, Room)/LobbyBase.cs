using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Linq;

public class LobbyBase : MonoBehaviour
{
    public TMP_Text Main_nickname;
    public TMP_Text Main_level;
    public RawImage Main_steamimage;
    public TMP_Text Main_cashpoint;
    public TMP_Text Main_basepoint;

    public GameObject[] characterPrefabs;

    private GameObject currentCharacter;

    public Button RoomleftButton;
    public Button RoomrightButton;
    public TMP_InputField RoomSearchInput;
    public Button RoomSearchButton;
    public Button RoomReloadButton;
    private int currentPage = 0;
    public TMP_Text CurrentRoomNum;
    public RectTransform RoomContentPanel;
    public GameObject RoomEntryPrefab;

    public UnityEvent onReLoadRoom;
    public UnityEvent<Room> onSendRoom;

    public Button Exit_Button;

    public UISoundManager UisoundManager;
    
    void Start()
    {
        /*UI에 이미지, 레벨 등 기입*/
        Main_nickname.text = ClientDataManager.Instance.UserDetails.Nickname;
        Main_level.text = $"LV. {ClientDataManager.Instance.UserDetails.Level}";
        Main_steamimage.texture =  ClientDataManager.Instance.UserTexture;
        Main_cashpoint.text = $"{ClientDataManager.Instance.UserDetails.cashpoint.ToString("N0")}";
        Main_basepoint.text = $"{ClientDataManager.Instance.UserDetails.basepoint.ToString("N0")}";
        
        if(int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out int characterID))
        {
            CreateCharacter(characterID);
        }

        RoomrightButton.onClick.AddListener(NextPage);
        RoomleftButton.onClick.AddListener(PreviousPage);
        RoomSearchInput.onEndEdit.AddListener(OnSearchInputSubmitted);
        RoomSearchButton.onClick.AddListener(OnSearchInputWithButton);
        RoomReloadButton.onClick.AddListener(OnButtonReLoadButton);
        UpdateRoomList();

        Exit_Button.onClick.AddListener(OnGameExit);

        UisoundManager = GameObject.Find("UI_SoundObject").GetComponent<UISoundManager>();
    }

    void CreateCharacter(int characterID)
    {
        if (currentCharacter != null)
            Destroy(currentCharacter);

        /*대표 캐릭터에 맞게 캐릭터 배치*/
        if (characterID > 0 && characterID <= characterPrefabs.Length)
        {
            GameObject prefab = characterPrefabs[characterID - 1];
            currentCharacter = Instantiate(prefab);
        }
        
        /*대표 캐릭터의 의상 적용*/
        foreach (var characterData in ClientDataManager.Instance.CharacterData.characters)
        {
            if (characterData.CharacterType == characterID)
            {
                if (characterData.TopOutfitID != "")
                    ApplyOutfit(characterData.TopOutfitID, currentCharacter, "Top", characterID);
                if (characterData.BottomOutfitID != "")
                    ApplyOutfit(characterData.BottomOutfitID, currentCharacter, "Bottom", characterID);
                if (characterData.ShoesOutfitID != "")
                    ApplyOutfit(characterData.ShoesOutfitID, currentCharacter, "Shoes", characterID);
                if (characterData.AllInOneOutfitID != "")
                    ApplyOutfit(characterData.AllInOneOutfitID, currentCharacter, "All_in_one", characterID);
                if (characterData.Accessory1ID == "")
                    Debug.Log("ACC1ID is Null!!");

                break;
            }
        }
    }

    /*캐릭터의 의상오브젝트의 자식오브젝트 중 동일한 의상 활성화*/
    void ApplyOutfit(string outfitID, GameObject character, string outType, int characterNum)
    {
        if(int.TryParse(outfitID, out int outfitIDin))
        {
            foreach (var outfit in ClientDataManager.Instance.OutfitData.outfits)
            {
                if (outfit.OutfitID == outfitIDin && outfit.Type == outType && outfit.Character_costume == characterNum)
                {
                    Transform categoryTransform = character.transform.Find(outType);
                    if (categoryTransform != null)
                    {
                        foreach (Transform child in categoryTransform)
                        {
                            bool shouldEnable = child.name == outfit.Description;
                            child.gameObject.SetActive(shouldEnable);
                        }
                    }
                }
            }
        }
        
    }

    /*방 목록 업데이트*/
    public void UpdateRoomList(string filter = "")
    {
        foreach (Transform child in RoomContentPanel)
        {
            Destroy(child.gameObject);
        }
        List<Room> filteredRooms = RoomManager.Instance.rooms;
        /*검색 값이 있을 경우 방 이름에 값이 있는 것 들만 선별*/
        if (!string.IsNullOrEmpty(filter))
        {
            filteredRooms = filteredRooms.Where(room => room.RoomName.ToLower().Contains(filter.ToLower())).ToList();
        }
        /*방을 시작하지 않은 방과 방 번호가 낮은 순서로 정렬*/
        filteredRooms = filteredRooms
            .OrderBy(room => room.RoomStart)
            .ThenBy(room => room.RoomNumber)
            .ToList();

        /*한 페이지에 총 14개의 방 표기*/
        int startRoom = currentPage * 14;
        int endRoom = Mathf.Min((currentPage + 1) * 14, filteredRooms.Count);

        /*현재 페이지의 방들에 대한 입장 기능과 방 이름, 번호, 인원을 표기*/
        for (int i = startRoom; i < endRoom; i++)
        {
            var room = filteredRooms[i];
            GameObject roomEntry = Instantiate(RoomEntryPrefab, RoomContentPanel);
            RoomEntry roomEntryScript = roomEntry.GetComponent<RoomEntry>();
            roomEntryScript.RoomData = room;
            roomEntryScript.onEnterRoom.AddListener(() => EnterRoom(room));

            TMP_Text roomNumberText = roomEntry.transform.Find("Room_Num").GetComponent<TMP_Text>();
            roomNumberText.text = room.RoomNumber.ToString();

            TMP_Text roomNameText = roomEntry.transform.Find("Room_Name").GetComponent<TMP_Text>();
            roomNameText.text = room.RoomName;

            TMP_Text roomCurrentPText = roomEntry.transform.Find("Room_Current").GetComponent<TMP_Text>();
            roomCurrentPText.text = room.CurrentRoomPNumber.ToString();

            TMP_Text roomMaxPText = roomEntry.transform.Find("Room_Max").GetComponent<TMP_Text>();
            roomMaxPText.text = room.MaxRoomPNumber.ToString();

            GameObject roomisStart = roomEntry.transform.Find("Panel_InGame").gameObject;
            roomisStart.SetActive(room.RoomStart);

            /*시작한 방의 경우 마우스 오버 사운드 재생하지 않음*/
            if (!roomisStart.activeSelf)
                AddMouseOverSound(roomEntry);

            /*비밀번호가 있는 방과 없는 방의 button 오브젝트가 다르기 때문에 모두 적용*/
            AddButtonClickSound(roomEntryScript.ButtonNonePass.GetComponent<Button>());
            AddButtonClickSound(roomEntryScript.ButtonHasPass.GetComponent<Button>());
        }
        UpdatePageDisplay(filteredRooms.Count);
    }
    /*현재 페이지 표기, 이동 가능한 페이지를 계산하여 button 비활성화*/
    void UpdatePageDisplay(int roomCount)
    {
        CurrentRoomNum.text = $"{currentPage + 1}";
        int totalPages = (roomCount + 14 - 1) / 14;
        RoomleftButton.interactable = currentPage > 0;
        RoomrightButton.interactable = currentPage < totalPages - 1;
    }
    /*방 목록 다음 페이지 이동*/
    public void NextPage()
    {
        int totalPages = (RoomManager.Instance.rooms.Count + 14 - 1) / 14;
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            UpdateRoomList();
        }
    }
    /*방 목록 이전 페이지 이동*/
    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdateRoomList();
        }
    }
    /*Enter 키로 방 검색*/
    public void OnSearchInputSubmitted(string input)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string searchQuery = input.Trim();
            currentPage = 0;
            UpdateRoomList(searchQuery);
            RoomSearchInput.text = "";
        }
    }
    /*버튼 클릭으로 방 검색*/
    public void OnSearchInputWithButton()
    {
        string searchQuery = RoomSearchInput.text.Trim();
        currentPage = 0;
        UpdateRoomList(searchQuery);
        RoomSearchInput.text = "";
    }
    /*새로고침 시 방 목록을 다시 가져옴 onReLoadRoom 이벤트는 Lobby_Client에서 적용*/
    public void OnButtonReLoadButton()
    {
        onReLoadRoom?.Invoke();
        RoomReloadButton.interactable = false;
        StartCoroutine(EnableButtonsAfterDelay(5));
    }
    /*연속적인 새로고침 방지*/
    IEnumerator EnableButtonsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RoomReloadButton.interactable = true;
    }
    /*onSendRoom 이벤트는 Lobby_Client에서 적용*/
    void EnterRoom(Room room)
    {
        onSendRoom?.Invoke(room);
    }

    public void OnGameExit()
    {
	Application.Quit();
    }

    private void AddMouseOverSound(GameObject roomEntry)
    {
        EventTrigger trigger = roomEntry.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = roomEntry.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        entry.callback.AddListener((eventData) => {
            UisoundManager?.PlayMouseOverSound();
        });
        trigger.triggers.Add(entry);
    }

    private void AddButtonClickSound(Button button)
    {
        if (button != null)
        {
            button.onClick.AddListener(() => {
                UisoundManager?.PlaySuccessBtnSound();
            });
        }
    }
}
