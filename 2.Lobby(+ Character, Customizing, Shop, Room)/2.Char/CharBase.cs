using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;

[System.Serializable]
public class CharInfoCharacterData
{
    public string family;
    public string name;
    public string explanation;
    public string attackPower;
    public string attackSpeed;
    public string attackRange;

    public Sprite skill1;
    public Sprite skill2;
    public Sprite skill3;
    public Sprite skill4;

    public string skill1name;
    public string skill1description;
    public string skill2name;
    public string skill2description;
    public string skill3name;
    public string skill3description;
    public string skill4name;
    public string skill4description;

    public AudioClip CharacterAudio;
}

public class CharBase : MonoBehaviour
{
    public GameObject[] cha_imgs;
    public Button[] cha_buttons;
    public CharInfoCharacterData[] characterDataArray;

    public TMP_Text ChaFam;
    public TMP_Text ChaName;
    public TMP_Text ChaExplan;
    public TMP_Text ChaAPower;
    public TMP_Text ChaASpeed;
    public TMP_Text ChaARange;

    public Image Skill_1;
    public Image Skill_2;
    public Image Skill_3;
    public Image Skill_4;

    public TMP_Text Main_cashpoint;
    public TMP_Text Main_basepoint;

    public int Index_Send;
    public GameObject tooltipPanel;
    public Image tooltipImage;
    public TextMeshProUGUI tooltipTitleText;
    public TextMeshProUGUI tooltipDescriptionText;
    public int? currentlySelectedSkillIndex = null;
    
    void Start()
    {
        /*초기 캐릭터 정보를 메인캐릭터 정보로 설정*/
        int mainCharacterId;

        if (int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out mainCharacterId))
        {
            int imageIndex = mainCharacterId-1;
            
            ActiveImags(imageIndex);
        }
        else
        {
            Debug.LogError("MainCharacterID is not a valid integer");
        }
        /*캐릭처창에서의 캐시, 인게임재화 표기*/
        Main_cashpoint.text = $"{ClientDataManager.Instance.UserDetails.cashpoint.ToString("N0")}";
        Main_basepoint.text = $"{ClientDataManager.Instance.UserDetails.basepoint.ToString("N0")}";

        /*캐릭터의 스킬 이미지 네개를 표기하고 PointerEenter 시 각 스킬에 맞는 정보를 툴팁에 표기하기 위해 준비
         마우스가 스킬 이미지를 벗어났을 경우 툴팁 숨기기
         (툴팁을 열었을 경우 마우스가 스킬 이미지를 벗어나 툴팁으로 이동하여 툴팁이 꺼지는 현상을 Pivot(1.01,1.01)로 수정해서 해결)*/
        Image[] skillImages = { Skill_1, Skill_2, Skill_3, Skill_4 };
        for (int i = 0; i < skillImages.Length; i++)
        {
            EventTrigger trigger = skillImages[i].gameObject.GetComponent<EventTrigger>() ?? skillImages[i].gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            int skillIndex = i;
            entryEnter.callback.AddListener((data) => { ShowSkillTooltip(skillIndex); });
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data) => { HideSkillTooltip(); });
            trigger.triggers.Add(entryExit);
        }

        tooltipPanel.SetActive(false);
    }

    void Update()
    {
        /*툴팁의 위치를 마우스의 위치에 따라 움직이게 하기 위함*/
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (tooltipPanel.transform.parent as RectTransform),
                Input.mousePosition, 
                null,
                out localPoint
            );

            tooltipPanel.transform.localPosition = localPoint;
        }
        if (tooltipPanel.activeSelf && currentlySelectedSkillIndex.HasValue)
        {
            ShowSkillTooltip(currentlySelectedSkillIndex.Value);
        }
    }

    /*툴팁 표기 방식*/
    void ShowSkillTooltip(int itemC)
    {
        currentlySelectedSkillIndex = itemC;
        string skillName = "";
        Sprite skillSprite = null;
        string skillDescription = "";

        switch (itemC)
        {
            case 0:
                skillName = characterDataArray[Index_Send].skill1name;
                skillSprite = characterDataArray[Index_Send].skill1;
                skillDescription = characterDataArray[Index_Send].skill1description;
                break;
            case 1:
                skillName = characterDataArray[Index_Send].skill2name;
                skillSprite = characterDataArray[Index_Send].skill2;
                skillDescription = characterDataArray[Index_Send].skill2description;
                break;
            case 2:
                skillName = characterDataArray[Index_Send].skill3name;
                skillSprite = characterDataArray[Index_Send].skill3;
                skillDescription = characterDataArray[Index_Send].skill3description;
                break;
            case 3:
                skillName = characterDataArray[Index_Send].skill4name;
                skillSprite = characterDataArray[Index_Send].skill4;
                skillDescription = characterDataArray[Index_Send].skill4description;
                break;
            default:
                Debug.LogError("Invalid skill index");
                return;
        }

        tooltipTitleText.text = skillName;
        tooltipImage.sprite = skillSprite;
        tooltipDescriptionText.text = skillDescription;

        tooltipPanel.SetActive(true);
        /*툴팁의 크기를 설명의 길이에 따라 늘어나고 줄어들게 할 수 있기 위함*/
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel.GetComponent<RectTransform>());
    }
    /*툴팁을 숨기고 currentlySelectedSkillIndex를 null로 변경하여 Update()에서 툴팁 갱신안하게 함*/
    void HideSkillTooltip()
    {
        tooltipPanel.SetActive(false);
        currentlySelectedSkillIndex = null;
    }
    /*현재 선택한 캐릭터를 selection으로 표시해주는데 이를 전부 숨김*/
    public void DeactiveImage()
    {
        foreach(GameObject cha_obj in cha_imgs)
        {
            cha_obj.SetActive(false);
        }
        foreach (Button btn in cha_buttons)
        {
            Transform selection = btn.transform.Find("Selection");
            if (selection != null)
            {
                selection.gameObject.SetActive(false);
            }
        }
    }
    /*이미지 표기, 선택한 캐릭터 표시*/
    public void ActiveImags(int index)
    {
        DeactiveImage();
        if(index >= 0 && index < cha_imgs.Length)
        {
            /*cha_imgs[index]로 선택한 캐릭터의 전체 이미지를 활성화*/
            cha_imgs[index].SetActive(true);
            Index_Send = index;

            /*선택한 캐릭터 얼굴 이미지에 선택함을 표시*/
            Transform selection = cha_buttons[index].transform.Find("Selection");
            if (selection != null)
            {
                selection.gameObject.SetActive(true);
            }
            SetCharacterData(index);
        }
    }
    /*캐릭터 별 정보를 표기*/
    private void SetCharacterData(int index)
    {
        if (index >= 0 && index < characterDataArray.Length)
        {
            CharInfoCharacterData data = characterDataArray[index];
            /*특성에 따라 색상을 달리하여 표기*/
            ChaFam.text = data.family;
            if (data.family == "Dark")
            {
                ChaFam.color = new Color32(203, 120, 207, 255); // #CB78CF
            }
            else if (data.family == "Light")
            {
                ChaFam.color = new Color32(255, 240, 146, 255); // #FFF092
            }
            /*캐릭터 이름, 대사, 능력치 표기*/
            ChaName.text = data.name;
            ChaExplan.text = data.explanation;
            ChaAPower.text = data.attackPower;
            ChaASpeed.text = data.attackSpeed;
            ChaARange.text = data.attackRange;

            /*스킬 이미지 표기*/
            Skill_1.sprite = data.skill1;
            Skill_2.sprite = data.skill2;
            Skill_3.sprite = data.skill3;
            Skill_4.sprite = data.skill4;

            /*캐릭터를 선택 시 해당 캐릭터의 메인 대사 사운드 재생*/
            InGameSoundManager.Instance?.CharCharacterSound(data.CharacterAudio);
        }
        else
        {
            Debug.LogError("Invalid character index");
        }
    }
}
