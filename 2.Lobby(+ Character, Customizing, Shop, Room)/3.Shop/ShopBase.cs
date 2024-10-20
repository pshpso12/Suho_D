using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.Events;
using System.Linq;
using UnityEngine.EventSystems;

public class ShopBase : MonoBehaviour
{
    private int currentItemIndex = 0;
    public Button[] buttons;

    public Color selectedColor = new Color(1f, 1f, 1f, 1f);
    public Color defaultColor = new Color(0.666f, 0.666f, 0.666f, 1f);

    public TMP_Text Main_cashpoint;
    public TMP_Text Main_basepoint;

    public GameObject itemPrefab;
    public Transform itemsPanel;
    public int currentIndexList = 0;
    
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
    public UnityEvent<string> onSendItem;
    private Dictionary<int, GameObject> outfitButtons = new Dictionary<int, GameObject>();
    public ShopCharacter shopcharacter;

    public OutfitDataList outfitDataList;

    public UISoundManager UisoundManager;

    void Start()
    {
        if(int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out int characterID))
        {
            CharacterNum = characterID;
        }
        /*전체, 상의, 하의, 신발, 악세사리 중 처음은 전체로 설정*/
        ActivateObject(0);

        /*상점 페이지에서 재화 표기*/
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
        DeactivateAll();

        if (index >= 0 && index < buttons.Length)
        {
            /*선택한 버튼의 색만 흰색으로 변경*/
            SetButtonColor(buttons[index], selectedColor);
        }
        currentIndexList = index;
        OutputOutfit_Shop(index);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        TMP_Text buttonText = btn.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.color = color;
        }
    }

    private void OutputOutfit_Shop(int index)
    {
        var characterData = ClientDataManager.Instance.CharacterData.characters.Find(c => c.CharacterType == CharacterNum);
        foreach (Transform child in itemsPanel)
        {
            Destroy(child.gameObject);
        }
        /*보유 중인 의상은 배치하지 않기 위함*/
        HashSet<string> existingDescriptions = new HashSet<string>();
        foreach (var outfit in ClientDataManager.Instance.OutfitData.outfits)
        {
            if (outfit.Character_costume == CharacterNum)
            {
                existingDescriptions.Add(outfit.Description);
            }
        }

        foreach (var outfitInData in outfitDataList.outfits)
        {
            if (outfitInData.CharaterNum == CharacterNum && !existingDescriptions.Contains(outfitInData.image.name))
            {
                switch (index)
                {
                    case 0:
                        DisPlayItems(outfitInData);
                        break;
                    case 1:
                        if (outfitInData.outfit_Type == "Top")
                            DisPlayItems(outfitInData);
                        break;
                    case 2:
                        if (outfitInData.outfit_Type == "Bottom")
                            DisPlayItems(outfitInData);
                        break;
                    case 3:
                        if (outfitInData.outfit_Type == "Shoes")
                            DisPlayItems(outfitInData);
                        break;
                    case 4:
                        break;
                    case 5:
                        if (outfitInData.outfit_Type != "Top" && outfitInData.outfit_Type != "Bottom" && outfitInData.outfit_Type != "Shoes")
                            DisPlayItems(outfitInData);
                        break;
                }
            }
        }
    }
    
    private void DisPlayItems(OutfitData outfitData)
    {
        GameObject newItem = Instantiate(itemPrefab, itemsPanel);
        /*의상 버튼을 클릭 시 미리 의상을 입어볼 수 있게 함*/
        newItem.GetComponent<Button>().onClick.AddListener(() => OnButtonChangeOutfit(outfitData));

        /*구매 버튼 클릭 시 의상 구매 메시지 보내기*/
        Button buttonBuy = newItem.transform.Find("Button_Buy").GetComponent<Button>();
        buttonBuy.onClick.AddListener(() => OnButtonSendItem(outfitData));

        /*이미지와 이름, 가격 배치*/
        Image outfitImage = newItem.transform.Find("Image_Outfit").GetComponent<Image>();
        outfitImage.sprite = outfitData.image;
        
        TMP_Text itemNameText = newItem.transform.Find("Item_Name").GetComponent<TMP_Text>();
        itemNameText.text = outfitData.outfitName;

        TMP_Text itemPriceText = newItem.transform.Find("Item_Price").GetComponent<TMP_Text>();
        itemPriceText.text = outfitData.prices[0].price.ToString("N0");

        GameObject bpImage = newItem.transform.Find("BP").gameObject;
        GameObject cpImage = newItem.transform.Find("CP").gameObject;

        bpImage.SetActive(outfitData.prices[0].price_Type != "CP");
        cpImage.SetActive(outfitData.prices[0].price_Type == "CP");

        GameObject itemImgae2 = newItem.transform.Find("Image_2").gameObject;
        TMP_Text itemPriceText2 = newItem.transform.Find("Item_Price_2").GetComponent<TMP_Text>();
        GameObject bpImage2 = newItem.transform.Find("BP_2").gameObject;
        GameObject cpImage2 = newItem.transform.Find("CP_2").gameObject;
        GameObject itemImgae3 = newItem.transform.Find("Image_3").gameObject;
        TMP_Text itemPriceText3 = newItem.transform.Find("Item_Price_3").GetComponent<TMP_Text>();
        GameObject bpImage3 = newItem.transform.Find("BP_3").gameObject;
        GameObject cpImage3 = newItem.transform.Find("CP_3").gameObject;

        /*가격은 1~3개 있을 수 있기 때문에 이용*/
        if(outfitData.prices.Count == 1)
        {
            itemImgae2.SetActive(false);
            itemPriceText2.gameObject.SetActive(false);
            bpImage2.SetActive(false);
            cpImage2.SetActive(false);
            itemImgae3.SetActive(false);
            itemPriceText3.gameObject.SetActive(false);
            bpImage3.SetActive(false);
            cpImage3.SetActive(false);
        }
        else if(outfitData.prices.Count == 2)
        {
            itemPriceText2.text = outfitData.prices[1].price.ToString("N0");
            bpImage2.SetActive(outfitData.prices[1].price_Type != "CP");
            cpImage2.SetActive(outfitData.prices[1].price_Type == "CP");
            itemImgae3.SetActive(false);
            itemPriceText3.gameObject.SetActive(false);
            bpImage3.SetActive(false);
            cpImage3.SetActive(false);
        }
        else if(outfitData.prices.Count == 3)
        {
            itemPriceText2.text = outfitData.prices[1].price.ToString("N0");
            bpImage2.SetActive(outfitData.prices[1].price_Type != "CP");
            cpImage2.SetActive(outfitData.prices[1].price_Type == "CP");
            itemPriceText3.text = outfitData.prices[2].price.ToString("N0");
            bpImage3.SetActive(outfitData.prices[2].price_Type != "CP");
            cpImage3.SetActive(outfitData.prices[2].price_Type == "CP");
        }

        AddButtonClickSound(newItem.GetComponent<Button>(), false);
        AddButtonClickSound(newItem.transform.Find("Button_Buy").GetComponent<Button>(), true);
    }

    void OnButtonChangeOutfit(OutfitData outfitData)
    {
        shopcharacter.ChnageCharacterOutfit(outfitData.outfit_Type, outfitData.image.name);
    }

    /*Lobby_Client에서 HandleSendItem을 진행하여 구매 의상 팝업을 생성*/
    void OnButtonSendItem(OutfitData outfitData)
    {
        string pricesString = string.Join("&", outfitData.prices.Select(p => $"{p.price_Type},{p.price}").ToArray());

        string message = $"{outfitData.CharaterNum};{outfitData.outfit_Type};{outfitData.image.name};{outfitData.outfitName};{pricesString}";
        onSendItem?.Invoke(message);
    }
    /*의상 구매 후에 재화를 새로고침*/
    public void ReLoadPoint()
    {
        Main_cashpoint.text = $"{ClientDataManager.Instance.UserDetails.cashpoint.ToString("N0")}";
        Main_basepoint.text = $"{ClientDataManager.Instance.UserDetails.basepoint.ToString("N0")}";
    }
    /*의상 구매 완료 후 캐릭터 회전 값 초기화 및 의상 적용*/
    public void ReLoadCha()
    {
        shopcharacter.ReloadaCharacter(shopcharacter.currentChaNum);
    }

    private void AddButtonClickSound(Button button, bool Success)
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
                if(Success)
                    UisoundManager?.PlaySuccessBtnSound();
                else
                    UisoundManager?.PlayClick_NextSound();
            });
        }
    }
}
