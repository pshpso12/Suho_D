using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CusBase : MonoBehaviour
{
    private int currentItemIndex = 0;
    public Button[] buttons;

    public Color selectedColor = new Color(1f, 1f, 1f, 1f);
    public Color defaultColor = new Color(0.666f, 0.666f, 0.666f, 1f);

    public TMP_Text Main_cashpoint;
    public TMP_Text Main_basepoint;

    public TMP_Text currentPage;
    public TMP_Text maxPage;
    public Button Lpage_Btn;
    public Button Rpage_Btn;

    public GameObject itemPrefab;
    public Transform itemsPanel;
    [System.Serializable]
    public class OutfitInfo
    {
        public int OutfitID;
        public string Description;
        public string Type;
        public int Character_costume;
        public bool IsWorn;
    }
    private List<OutfitInfo> All_Outlist = new List<OutfitInfo>();
    private int currentPageIndex = 0;

    public int CharacterNum = 0;
    public UnityEvent<string> onSendCloth;
    private Dictionary<int, GameObject> outfitButtons = new Dictionary<int, GameObject>();
    public CusCharacter cuscharacter;

    public UISoundManager UisoundManager;

    void Start()
    {
        if(int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out int characterID))
        {
            CharacterNum = characterID;
        }
        /*전체, 상의, 하의, 신발, 악세사리 중 처음은 전체로 설정*/
        ActivateObject(0);
        
        /*커스터마이징 페이지에서 재화 표기*/
        Main_cashpoint.text = $"{ClientDataManager.Instance.UserDetails.cashpoint.ToString("N0")}";
        Main_basepoint.text = $"{ClientDataManager.Instance.UserDetails.basepoint.ToString("N0")}";
        
        int mainCharacterId;

        /*초기 캐릭터 정의 ((mainCharacterId-1) / 10인 이유는 초기 캐릭터가 40개에서 10개로 변경하면서 의미없는 코드지만
        이후, 캐릭터 추가를 위해 코트는 그대로 유지해두었습니다.)*/
        if (int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out mainCharacterId))
        {
            currentItemIndex = (mainCharacterId-1) / 10;
        }

        UisoundManager = GameObject.Find("UI_SoundObject").GetComponent<UISoundManager>();
    }

    public void DeactivateAll()
    {
        foreach (Button btn in buttons)
        {
            SetButtonColor(btn, defaultColor);
        }
    }
    /*버튼에 따라 조건에 맞는 의상 배치*/
    public void ActivateObject(int index)
    {
        /*모든 버튼의 색을 회색으로 변경*/
        DeactivateAll();

        if (index >= 0 && index < buttons.Length)
        {
            /*선택한 버튼의 색만 흰색으로 변경*/
            SetButtonColor(buttons[index], selectedColor);
        }
        OutputOutfitDescriptionsByType(index);
    }
    
    private void SetButtonColor(Button btn, Color color)
    {
        TMP_Text buttonText = btn.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.color = color;
        }
    }
    /*모든 의상을 지우고 조건에 맞는 의상들을 배치*/
    private void OutputOutfitDescriptionsByType(int index)
    {
        All_Outlist.Clear();
        currentPageIndex = 0;
        var characterData = ClientDataManager.Instance.CharacterData.characters.Find(c => c.CharacterType == CharacterNum);

        foreach (var outfit in ClientDataManager.Instance.OutfitData.outfits)
        {
            if (outfit.Character_costume != CharacterNum)
                continue;

            /*현재 캐릭터가 입고 있는 의상에는 추가적인 표시를 하기 위함*/
            bool isWorn = outfit.OutfitID == (characterData.TopOutfitID != null ? int.Parse(characterData.TopOutfitID) : -1) ||
              outfit.OutfitID == (characterData.BottomOutfitID != null ? int.Parse(characterData.BottomOutfitID) : -1) ||
              outfit.OutfitID == (characterData.ShoesOutfitID != null ? int.Parse(characterData.ShoesOutfitID) : -1);

            switch (index)
            {
                /*전체*/
                case 0:
                    All_Outlist.Add(new OutfitInfo { 
                        Type = outfit.Type, 
                        OutfitID = outfit.OutfitID,
                        Description = outfit.Description, 
                        Character_costume = outfit.Character_costume,
                        IsWorn = isWorn
                    });
                    break;
                /*상의*/
                case 1:
                    if (outfit.Type == "Top")
                        All_Outlist.Add(new OutfitInfo { 
                            Type = outfit.Type, 
                            OutfitID = outfit.OutfitID,
                            Description = outfit.Description, 
                            Character_costume = outfit.Character_costume,
                            IsWorn = isWorn
                        });
                    break;
                /*하의*/
                case 2:
                    if (outfit.Type == "Bottom")
                        All_Outlist.Add(new OutfitInfo { 
                            Type = outfit.Type, 
                            OutfitID = outfit.OutfitID,
                            Description = outfit.Description, 
                            Character_costume = outfit.Character_costume,
                            IsWorn = isWorn
                        });
                    break;
                /*신발*/
                case 3:
                    if (outfit.Type == "Shoes")
                        All_Outlist.Add(new OutfitInfo { 
                            Type = outfit.Type, 
                            OutfitID = outfit.OutfitID,
                            Description = outfit.Description, 
                            Character_costume = outfit.Character_costume,
                            IsWorn = isWorn
                        });
                    break;
                /*악세사리 탭이지만 악세사리는 현재 없음*/
                case 4:
                    Debug.Log("Outfit Description: " + outfit.Description);
                    break;
                /*기타 - 이후 추가를 위해*/
                case 5:
                    if (outfit.Type != "Top" && outfit.Type != "Bottom" && outfit.Type != "Shoes")
                        All_Outlist.Add(new OutfitInfo { 
                            Type = outfit.Type, 
                            OutfitID = outfit.OutfitID,
                            Description = outfit.Description, 
                            Character_costume = outfit.Character_costume,
                            IsWorn = isWorn
                        });
                    break;
            }
        }
        UpdatePagination();
    }

    /*의상은 페이지 별로 24개 배치 가능(로비의 방과 동일한 동작)*/
    private void UpdatePagination()
    {
        int maxPages = Mathf.Max(1, Mathf.CeilToInt((float)All_Outlist.Count / 24));
        currentPage.text = (currentPageIndex + 1).ToString();
        maxPage.text = maxPages.ToString();

        Lpage_Btn.interactable = currentPageIndex > 0;
        Rpage_Btn.interactable = currentPageIndex < (maxPages - 1);
        DisplayItemsForCurrentPage();
    }
    private void DisplayItemsForCurrentPage()
    {
        foreach (Transform child in itemsPanel)
        {
            Destroy(child.gameObject);
        }
        outfitButtons.Clear();

        int startIndex = currentPageIndex * 24;
        int endIndex = Mathf.Min(startIndex + 24, All_Outlist.Count);

        /*페이지에 맞는 의상 이미지 배치*/
        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject newItem = Instantiate(itemPrefab, itemsPanel);
            Sprite itemSprite = Resources.Load<Sprite>($"Images/{All_Outlist[i].Description}");
            if (itemSprite != null)
            {
                newItem.GetComponent<Image>().sprite = itemSprite;
                OutfitInfo currentOutfit = All_Outlist[i];
                newItem.GetComponent<Button>().onClick.AddListener(() => OnButtonSendCloth(currentOutfit));
                outfitButtons[currentOutfit.OutfitID] = newItem;
                /*입고 있는 의상은 하위 오브젝트를 활성화하여 표기, 버튼을 enabled로 변경하여 사운드와 입고 있는 의상으로 의상 변경을 막음*/
                if(All_Outlist[i].IsWorn == true)
                {
                    Button newItemButton = newItem.GetComponent<Button>();
                    newItemButton.enabled = false;
                    Transform childTransform = newItem.transform.GetChild(0);
                    childTransform.gameObject.SetActive(true);
                }

                AddButtonClickSound(newItem.GetComponent<Button>());
            }
        }
    }

    /*왼쪽,오른쪽 버튼에 direction을 -1, 1로 OnClick()이벤트 추가*/
    public void ChangePage(int direction)
    {
        currentPageIndex += direction;
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, Mathf.Max(0, Mathf.CeilToInt((float)All_Outlist.Count / 24) - 1));
        UpdatePagination();
    }

    /*의상 클릭 시 메시지를 보낸 후 의상 변경 및 캐릭터 의상 변경*/
    void OnButtonSendCloth(OutfitInfo clickedOutfit)
    {
        string message = $"{clickedOutfit.Character_costume};{clickedOutfit.Type};{clickedOutfit.OutfitID};{clickedOutfit.Description}";
        onSendCloth?.Invoke(message);
        foreach (var outfit in All_Outlist)
        {
            if (outfit.Type == clickedOutfit.Type)
            {
                if (outfit.IsWorn)
                {
                    outfit.IsWorn = false;
                    UpdateButtonState(outfit, false);
                }
            }
        }

        clickedOutfit.IsWorn = true;
        /*OutfitInfo 변경 후 의상 이미지에 적용*/
        UpdateButtonState(clickedOutfit, true);
        /*캐릭터의 의상을 변경*/
        cuscharacter.ChnageCharacterOutfit(clickedOutfit.Type, clickedOutfit.Description);
    }
    
    /*OutfitInfo 변경 후 의상 이미지에 적용*/
    void UpdateButtonState(OutfitInfo outfit, bool isWorn)
    {
        if (outfitButtons.TryGetValue(outfit.OutfitID, out GameObject buttonGameObject))
        {
            Button buttonComponent = buttonGameObject.GetComponent<Button>();
            Transform childTransform = buttonGameObject.transform.GetChild(0);
            if (isWorn)
            {
                buttonComponent.enabled = false;
                childTransform.gameObject.SetActive(true);
            }
            else
            {
                buttonComponent.enabled = true;
                childTransform.gameObject.SetActive(false);
            }
        }
    }

    private void AddButtonClickSound(Button button)
    {
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        entry.callback.AddListener((eventData) => {
            if (button.enabled)
            {
                UisoundManager?.PlayMouseOverSound();
            }
        });
        trigger.triggers.Add(entry);

        if (button != null)
        {
            button.onClick.AddListener(() => {
                UisoundManager?.PlaySuccessBtnSound();
            });
        }
    }
    
}
