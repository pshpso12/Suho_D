using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;

[System.Serializable]
public class SkillUi
{
    public GameObject Skill;
    public Image Skill_image;
    public GameObject Skill_NoneLock;
    public GameObject Skill_Lock;
}

[System.Serializable]
public class BuffImageEntry
{
    public string Key;
    public Sprite Value;
}
public class Select_Change : MonoBehaviour
{
    public GameObject Unit_info;
    public GameObject Unit_info_multi;

    public TextMeshProUGUI Info_name;
    public Image Info_Unit_image;
    public Image Info_Fam_image;
    
    public TextMeshProUGUI Info_Dam;
    public TextMeshProUGUI Info_Range;
    public TextMeshProUGUI Info_Speed;

    public Button U_Up;
    public Button U_Up_Pre;
    public Button U_Sell;

    public float[,] upgradeValues = new float[4, 4];

    public Image unitImagePrefab;
    public Transform gridLayoutGroup;
    private List<UnitManager_Net> lastUpdatedUnits = new List<UnitManager_Net>();
    public GameData myGamedata;
    private ButtonChildTint btnChild;

    public TextMeshProUGUI Info_Level;
    public TextMeshProUGUI Info_CurrentExp;
    public TextMeshProUGUI Info_MaxExp;
    public Slider Info_ExpSlider;

    public List<SkillUi> skills_Ui = new List<SkillUi>();

    public GameObject tooltipPanel;
    public Image tooltipImage;
    public TextMeshProUGUI tooltipTitleText;
    public TextMeshProUGUI tooltipDescriptionText;

    public Transform Buffs_G;
    public GameObject Buff_Pre;

    [SerializeField] private List<BuffImageEntry> buffImagesList = new List<BuffImageEntry>();
    private Dictionary<string, Sprite> buffImagesDictionary;

    public EtcTtip etctip;
    public int? currentlySelectedSkillIndex = null;   

    public bool CBossUpgrade = false; 

    void Start()
    {
        Unit_info.SetActive(false);
        Unit_info_multi.SetActive(false);
        U_Up.interactable = false;
        U_Up_Pre.interactable = false;
        foreach (var skillUi in skills_Ui) 
        {
            /*4개의 스킬 이미지에 대한 PointerEnter와 Exit 적용*/
            int index = skills_Ui.IndexOf(skillUi);
            EventTrigger trigger = skillUi.Skill.GetComponent<EventTrigger>() ?? skillUi.Skill.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data) => { ShowSkillTooltip(index); });
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data) => { HideSkillTooltip(); });
            trigger.triggers.Add(entryExit);
        }
        /*리스트를 딕셔너리로 변경 딕셔너리는 사전 할당이 불가능하여 사용*/
        buffImagesDictionary = buffImagesList.ToDictionary(entry => entry.Key, entry => entry.Value);

        tooltipPanel.SetActive(false);
    }

    void Update()
    {
        /*현재 선택한 유닛에 따라 UI 변경 진행*/
        HandleUIVisibility();

        /*tooltip의 위치를 포인터 위치로 조정*/
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
        /*tooltip에 내용을 출력*/
        if (tooltipPanel.activeSelf && currentlySelectedSkillIndex.HasValue)
        {
            ShowSkillTooltip(currentlySelectedSkillIndex.Value);
        }
    }

    private void HandleUIVisibility()
    {
        if (Globals_Net.SELECTED_UNITS.Count == 0)
        {
            /*정보창을 비활성화, 다른 캐릭터 관련 툴팁도 숨기고, Skill툴팁도 숨김*/
            Unit_info.SetActive(false);
            Unit_info_multi.SetActive(false);
            lastUpdatedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
            if (etctip.currentTooltipIndex.HasValue && 
            (etctip.currentTooltipIndex.Value == 6 || 
            etctip.currentTooltipIndex.Value == 7 || 
            etctip.currentTooltipIndex.Value == 8))
            {
                etctip.HideTooltip();
            }
            HideSkillTooltip();
        }
        /*선택 유닛이 하나일 경우*/
        else if (Globals_Net.SELECTED_UNITS.Count == 1)
        {
            /*골드가 없을 경우 유닛 클릭 시 업그레이드 버튼이 켜졌다 꺼지는 것을 방지하기 위해 초기에는 버튼 비활성화*/
            U_Up.interactable = false;
            U_Up_Pre.interactable = false;
            /*단일 유닛 정보창 활성화*/
            Unit_info.SetActive(true);
            Unit_info_multi.SetActive(false);

            UnitManager_Net selectedUnit = Globals_Net.SELECTED_UNITS[0];
            string Unit_Tag = selectedUnit.gameObject.tag;
            char lastChar = Unit_Tag[Unit_Tag.Length-1];
            Dam_Fam damFamComponent = selectedUnit.GetComponent<Dam_Fam>();
            if(damFamComponent != null)
            {
                /*이름, 종족, 강화, 레벨, 경험치 등의 정보를 출력*/
                int D_text = (int)(upgradeValues[(int)lastChar-49, (int)damFamComponent.charatcterRace]);
                float S_text = (upgradeValues[(int)lastChar-49, (int)damFamComponent.charatcterRace + 2]/100);
                
                int D_UText = (int)((damFamComponent.attackPower*myGamedata.upgradeA_Num_[selectedUnit.UnitUpgradeValue]/100));
                float S_UText = (damFamComponent.attack_Speed*myGamedata.upgradeS_Num_[selectedUnit.UnitUpgradeValue]/100);

                int D_BText = (int)selectedUnit.Buff_Power;
                float S_BText = selectedUnit.Buff_Speed;

                string upgradeColorHex = GetColorByUpgradeValue(selectedUnit.UnitUpgradeValue);
                Color gradColor = GetGradColor(damFamComponent.charatcterRace);
                string colorHex = ColorUtility.ToHtmlStringRGB(gradColor);
                /*강화 수치가 0이 아닌 경우 캐릭터 이름 앞에 강화 수치를 표시*/
                if(selectedUnit.UnitUpgradeValue == 0)
                {
                    Info_name.text = "<color=#" + colorHex + ">[" + damFamComponent.charatcterRace.ToString() + "]</color>  " + damFamComponent.Unit_name;
                }
                else
                {
                    Info_name.text = "<color=#" + upgradeColorHex + ">+ " + selectedUnit.UnitUpgradeValue + "</color>" + "  <color=#" + colorHex + ">[" + damFamComponent.charatcterRace.ToString() + "]</color>  " + damFamComponent.Unit_name;
                }
                /*캐릭터 이미지와 종족 이미지 배치*/
                Info_Unit_image.sprite = damFamComponent.Unit_image;
                Info_Fam_image.sprite = damFamComponent.Race_image;

                Info_Range.text = selectedUnit.AttackRange.ToString();
                int attackPower = (int)(damFamComponent.attackPower / damFamComponent.DivNum);

                /*강화공격력, 종족공격력, 버프공격력은 없으면 표시하지않고 있으면 표시하기 위함*/
                string damageBonusText = D_text > 0 ? "<color=#5BC378> (+ " + D_text.ToString() + ")</color>" : "";
                string damageUpgradeText = D_UText > 0 ? "<color=#D1D747> (+ " + D_UText.ToString() + ")</color>" : "";
                string damageBuffText = D_BText > 0 ? "<color=#C35BB2> (+ " + D_BText.ToString() + ")</color>" : "";
                Info_Dam.text = attackPower.ToString() + damageBonusText + damageUpgradeText + damageBuffText;

                /*강화공격속도, 종족공격속도, 버프공격속도는 없으면 표시하지않고 있으면 표시하기 위함*/
                string speedBonusText = S_text > 0.001f ? "<color=#5BC378> (+ " + S_text.ToString("F2") + ")</color>" : "";
                string speedUpgradeText = S_UText > 0.001f ? "<color=#D1D747> (+ " + S_UText.ToString("F2") + ")</color>" : "";
                string speedBuffText = S_BText > 0.001f ? "<color=#C35BB2> (+ " + S_BText.ToString("F2") + ")</color>" : "";
                Info_Speed.text = damFamComponent.attack_Speed.ToString("F2") + speedBonusText + speedUpgradeText + speedBuffText;
                /*스킬 이미지 4개를 배치*/
                for(int i = 0; i < 4; i++)
                {
                    skills_Ui[i].Skill_image.sprite = selectedUnit.skills[i].image;
                }
                /*유닛 레벨에 따라 스킬의 잠금 이미지를 활성화하거나, 비활성화 함*/
                UpdateSkillUI(selectedUnit.UnitLevel);
                /*레벨이 최대일 경우는 MAX로 표기*/
                if (selectedUnit.UnitLevel >= 16)
                {
                    Info_Level.text = "Lv. Max";
                }
                else if (selectedUnit.UnitLevel < 16)
                {
                    Info_Level.text = "Lv. " + selectedUnit.UnitLevel.ToString();
                }
                Info_CurrentExp.text = ((int)selectedUnit.UnitCurrentExp).ToString();
                Info_MaxExp.text = ((int)selectedUnit.UnitMaxExp).ToString();
                float expRatio = selectedUnit.UnitCurrentExp / selectedUnit.UnitMaxExp;
                Info_ExpSlider.value = expRatio;
                /*최대 강화 달성 시 강화 버튼 비활성화*/
                if(selectedUnit.UnitUpgradeValue >= 30)
                {
                    U_Up.gameObject.SetActive(false);
                    U_Up_Pre.gameObject.SetActive(false);
                }
                else
                {
                    U_Up.gameObject.SetActive(true);
                    U_Up_Pre.gameObject.SetActive(true);
                    if(myGamedata.Gold >= (myGamedata.upgradeCosts_[selectedUnit.UnitUpgradeValue]))
                        U_Up.interactable = true;
                    if(selectedUnit.UnitUpgradeValue < 10 && myGamedata.Gold >= (myGamedata.upgradeCosts_[selectedUnit.UnitUpgradeValue]))
                        U_Up_Pre.interactable = true;
                    if(selectedUnit.UnitUpgradeValue >= 10 && myGamedata.Gold >= (myGamedata.upgradeCosts_[selectedUnit.UnitUpgradeValue]) * 5)
                        U_Up_Pre.interactable = true;
                }

                if(Buffs_G)
                {
                    foreach (Transform child in Buffs_G)
                    {
                        Destroy(child.gameObject);
                    }
                    var sortedKeys = selectedUnit.Buff_Count.Keys.OrderBy(k => k).ToList();
                    /*버프 이미지와 "X 2"와 같이 개수를 표시*/
                    foreach(var key in sortedKeys)
                    {
                        if(selectedUnit.Buff_Count[key] != 0)
                        {
                            GameObject newBuffs = Instantiate(Buff_Pre, Buffs_G.transform);
                            Image BuffImage = newBuffs.GetComponent<Image>();
                            TMP_Text BuffText = newBuffs.GetComponentInChildren<TMP_Text>();

                            if(buffImagesDictionary.TryGetValue(key, out Sprite buffSprite))
                            {
                                BuffImage.sprite = buffSprite;
                            }
                            BuffText.text =  "X " + selectedUnit.Buff_Count[key];
                        }
                    }
                }
                lastUpdatedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
            }
        }
        else
        {
            Unit_info.SetActive(false);
            Unit_info_multi.SetActive(true);
            /*유닛을 1,2번 선택에서 1번 유닛 이미지를 두번 클릭 시 해당 유닛 단일 선택 UI로 변경되어야하는데
              리스트 비교를 안할 경우 Update로 계속 이미지를 지우고 생성하여 클릭이 안되는 현상을 막기위해 이전 선택과 현재 선택을 비교*/
            if(!ListsAreEqual(Globals_Net.SELECTED_UNITS, lastUpdatedUnits))
            {
                UpdateMultiUnitInfo();
                lastUpdatedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
            }
            /*캐릭터 관련 툴팁도 숨기고, Skill툴팁도 숨김*/
            if (etctip.currentTooltipIndex.HasValue && 
            (etctip.currentTooltipIndex.Value == 6 || 
            etctip.currentTooltipIndex.Value == 7 || 
            etctip.currentTooltipIndex.Value == 8))
            {
                etctip.HideTooltip();
            }
            HideSkillTooltip();
        }
    }
    /*캐릭터의 종족에 따라 색상을 다르게 표기하기 위함*/
    private Color GetGradColor(Dam_Fam.Race grad)
    {
        switch (grad)
        {
            case Dam_Fam.Race.Light:
                return new Color(255f / 255f, 255f / 255f, 146f / 255f);
            case Dam_Fam.Race.Dark:
                return new Color(250f / 255f, 146f / 255f, 255f / 255f);
            default:
                return new Color(170f / 255f, 170f / 255f, 170f / 255f);
        }
    }
    /*최고 등급 시 강화 생상 변경*/
    private string GetColorByUpgradeValue(int upgradeValue)
    {
        if (upgradeValue == 30)
        {
            return ColorUtility.ToHtmlStringRGB(new Color(255f / 255f, 215f / 255f, 0f / 255f));
        }
        else
        {
            return ColorUtility.ToHtmlStringRGB(new Color(255f / 255f, 255f / 255f, 255f / 255f));
        }
    }

    private void UpdateMultiUnitInfo()
    {
        /*존재하는 유닛 이미지 삭제*/
        foreach (Transform child in gridLayoutGroup)
        {
            Destroy(child.gameObject);
        }
        /*종족값을 기준으로 정렬*/
        var sortedUnits = Globals_Net.SELECTED_UNITS.OrderByDescending(unit =>
        {
            var damFamComponent = unit.GetComponent<Dam_Fam>();
            return damFamComponent != null ? (int)damFamComponent.charatcterRace : int.MinValue;
        }).ToList();
        /*배치 가능한 유닛의 수는 21개*/
        int unitCount = Mathf.Min(sortedUnits.Count, 21);
        for (int i = 0; i < unitCount; i++)
        {
            int currentIndex = i;
            Image newImage = Instantiate(unitImagePrefab, gridLayoutGroup);
            Dam_Fam damFamComponent = sortedUnits[i].GetComponent<Dam_Fam>();
            UnitManager_Net UmanagerComponent = sortedUnits[i].GetComponent<UnitManager_Net>();
            if (damFamComponent != null)
            {
                newImage.sprite = damFamComponent.Unit_image;
                Image childImage = newImage.transform.GetChild(0).GetComponent<Image>();
                /*테두리의 색을 종족에 맞게 조정*/
                Color currentColor = GetGradColor(damFamComponent.charatcterRace);
                currentColor.a *= 0.2f;
                childImage.color = currentColor;
                /*강화 수치를 이미지 위에 표시*/
                TMP_Text childText = newImage.transform.Find("Image/Up_text").GetComponent<TMP_Text>();
                if(UmanagerComponent.UnitUpgradeValue == 0)
                {
                    childText.text = "";
                }
                else
                {
                    childText.text = "+ " + UmanagerComponent.UnitUpgradeValue;
                }
                /*버튼 클릭 시 해당 캐릭터 단일 선택으로 변경*/
                Button newImageButton = newImage.GetComponent<Button>();
                newImageButton.onClick.AddListener(() => OnUnitImageClick(sortedUnits[currentIndex]));
            }
        }
    }
    /*리스트 비교*/
    private bool ListsAreEqual(List<UnitManager_Net> a, List<UnitManager_Net> b)
    {
        return a.SequenceEqual(b);
    }

    private void OnUnitImageClick(UnitManager_Net clickedUnit)
    {
        /*Shift를 누르고 클릭 시 해당 유닛을 선택 취소*/
        if (Input.GetKey(KeyCode.LeftShift))
        {
            clickedUnit.Deselect();
            UpdateMultiUnitInfo();
        }
        /*그냥 클릭 시 해당 유닛만 선택*/
        else
        {
            List<UnitManager_Net> selectedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
            foreach (UnitManager_Net um in selectedUnits)
            {
                um.Deselect();
            }
            clickedUnit._SelectUtil();
            UpdateMultiUnitInfo();
        }
    }

    void UpdateSkillUI(int unitLevel)
    {
        /*스킬 하나는 1레벨 부터 잠금해제 상태*/
        skills_Ui[0].Skill_image.color = new Color(255f/255f, 255f/255f, 255f/255f, 1);
        skills_Ui[0].Skill_NoneLock.SetActive(true);
        skills_Ui[0].Skill_Lock.SetActive(false);

        for (int i = 1; i < skills_Ui.Count; i++)
        {
            /*skills_Ui[1]은 2레벨에 잠금해제, skills_Ui[2]은 3레벨에 잠금해제...*/
            bool isSkillUnlocked = i < unitLevel;

            /*잠금 상태에서는 스킬 이미지를 회색조로 조정*/
            skills_Ui[i].Skill_image.color = isSkillUnlocked ? new Color(255f/255f, 255f/255f, 255f/255f, 1) : new Color(90f/255f, 90f/255f, 90f/255f, 1);
            skills_Ui[i].Skill_NoneLock.SetActive(isSkillUnlocked);
            skills_Ui[i].Skill_Lock.SetActive(!isSkillUnlocked);
        }
    }
    /*스킬 정보*/
    void ShowSkillTooltip(int itemC)
    {
        currentlySelectedSkillIndex = itemC;
        UnitManager_Net selectedUnit = Globals_Net.SELECTED_UNITS[0];
        Dam_Fam damFamComponent = selectedUnit.GetComponent<Dam_Fam>();
        string Unit_Tag = selectedUnit.gameObject.tag;
        char lastChar = Unit_Tag[Unit_Tag.Length-1];
        if(selectedUnit && damFamComponent)
        {
            /*밸런스 상 스킬 계수를 조정한 두 캐릭터의 스킬에 대해서 if, else if문으로 다르게 출력할 수 있게 조정
              다른 캐릭터들은 else문에서 출력*/
            if(damFamComponent.Unit_name == "xxx xxx" && itemC == 3)
            {
                if(selectedUnit.UnitLevel > itemC)
                {
                    int skillLevel = (selectedUnit.UnitLevel - itemC - 1) / 4 + 1;
                    tooltipTitleText.text = selectedUnit.skills[itemC].name + " <size=10>( Lv. " + skillLevel + " )</size>";
                    tooltipImage.sprite = selectedUnit.skills[itemC].image;
                    string updatedDescription = selectedUnit.skills[itemC].description;

                    if(itemC != 0)
                    {
                        string pattern1 = @"\[\]";
                        updatedDescription = Regex.Replace(selectedUnit.skills[itemC].description, pattern1, selectedUnit.skills[itemC].probabilities[skillLevel-1] + "%");
                    }
                    int matchCount = 0;
                    float D_text = ((upgradeValues[(int)lastChar-49, (int)damFamComponent.charatcterRace]) + ((damFamComponent.attackPower*myGamedata.upgradeA_Num_[selectedUnit.UnitUpgradeValue]/100)) + (damFamComponent.attackPower / damFamComponent.DivNum)) * (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f);
                    MatchEvaluator evaluator = m =>
                    {
                        matchCount++;
                        if(matchCount == 1)
                        {
                            return (int)(D_text*0.7f / 10f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 0.7f / 100f).ToString("F1") + ")</color></size>";
                        }
                        else if(matchCount == 2)
                        {
                            return (int)(D_text*0.9f / 10f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 0.9f / 100f).ToString("F1") + ")</color></size>";
                        }
                        else if(matchCount == 3)
                        {
                            return (int)(D_text*1f / 10f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 1f / 100f).ToString("F1") + ")</color></size>";
                        }
                        else if(matchCount == 4)
                        {
                            return (int)(D_text*1.2f / 10f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 1.2f / 100f).ToString("F1") + ")</color></size>";
                        }
                        else
                        {
                            return (int)(D_text*1.5f / 10f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 1.5f / 100f).ToString("F1") + ")</color></size>";
                        }
                    };
                    string pattern2 = @"\(\)";
                    updatedDescription = Regex.Replace(updatedDescription, pattern2, evaluator);
                    string pattern3 = @"\*\*";
                    updatedDescription = Regex.Replace(updatedDescription, pattern3, (50 + D_text/100).ToString("F1") + "% <size=8><color=#8989F6>(50% + 공격력의 " + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F1") + "%)</color></size>");
                    tooltipDescriptionText.text = updatedDescription;
                }
                else
                {
                    tooltipTitleText.text = selectedUnit.skills[itemC].name;
                    tooltipImage.sprite = selectedUnit.skills[itemC].image;
                    tooltipDescriptionText.text = "Lv. " + (itemC + 1).ToString() + " 에 활성화 됩니다.";
                }
            }
            else if(damFamComponent.Unit_name == "xxxx xxx" && itemC == 3)
            {
                if(selectedUnit.UnitLevel > itemC)
                {
                    int skillLevel = (selectedUnit.UnitLevel - itemC - 1) / 4 + 1;
                    tooltipTitleText.text = selectedUnit.skills[itemC].name + " <size=10>( Lv. " + skillLevel + " )</size>";
                    tooltipImage.sprite = selectedUnit.skills[itemC].image;
                    string updatedDescription = selectedUnit.skills[itemC].description;

                    if(itemC != 0)
                    {
                        string pattern1 = @"\[\]";
                        updatedDescription = Regex.Replace(selectedUnit.skills[itemC].description, pattern1, selectedUnit.skills[itemC].probabilities[skillLevel-1] + "%");
                    }
                    int matchCount = 0;
                    float D_text = ((upgradeValues[(int)lastChar-49, (int)damFamComponent.charatcterRace]) + ((damFamComponent.attackPower*myGamedata.upgradeA_Num_[selectedUnit.UnitUpgradeValue]/100)) + (damFamComponent.attackPower / damFamComponent.DivNum)) * (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f);
                    MatchEvaluator evaluator = m =>
                    {
                        matchCount++;
                        if(matchCount == 1)
                        {
                            return (int)(D_text*0.8f/ 6f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 0.8f / 100f).ToString("F1") + ")</color></size>";
                        }
                        else
                        {
                            return (int)(D_text*1.3f/ 6f) + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] * 1.3f / 100f).ToString("F1") + ")</color></size>";
                        }
                    };
                    string pattern2 = @"\(\)";
                    updatedDescription = Regex.Replace(updatedDescription, pattern2, evaluator);
                    string pattern3 = @"\*\*";
                    updatedDescription = Regex.Replace(updatedDescription, pattern3, (50 + D_text/100).ToString("F1") + "% <size=8><color=#8989F6>(50% + 공격력의 " + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F1") + "%)</color></size>");
                    tooltipDescriptionText.text = updatedDescription;
                }
                else
                {
                    tooltipTitleText.text = selectedUnit.skills[itemC].name;
                    tooltipImage.sprite = selectedUnit.skills[itemC].image;
                    tooltipDescriptionText.text = "Lv. " + (itemC + 1).ToString() + " 에 활성화 됩니다.";
                }
            }
            else
            {
                if(itemC == 0 || selectedUnit.UnitLevel > itemC)
                {
                    /*스킬레벨 잠금해제 된 경우에 ((Level - 스킬인덱스 - 1)/4) +1 로 수식화 가능(소수점 버림)*/
                    int skillLevel = (selectedUnit.UnitLevel - itemC - 1) / 4 + 1;
                    tooltipTitleText.text = selectedUnit.skills[itemC].name + " <size=10>( Lv. " + skillLevel + " )</size>";
                    tooltipImage.sprite = selectedUnit.skills[itemC].image;

                    string updatedDescription = selectedUnit.skills[itemC].description;

                    if(itemC != 0)
                    {
                        /*[]부분을 Replcae하여 스킬 발동 확률을 입력, 0번 스킬은 평타로 항상 발동*/
                        string pattern1 = @"\[\]";
                        updatedDescription = Regex.Replace(selectedUnit.skills[itemC].description, pattern1, selectedUnit.skills[itemC].probabilities[skillLevel-1] + "%");
                    }
                    /*()부분을 Replcae하여 (유닛공격력합 * 스킬공격력 / 100 / DivNum)을 입력, Buff로 올라간 공격력은 반영하지 않음*/
                    string pattern2 = @"\(\)";
                    float D_text = ((upgradeValues[(int)lastChar-49, (int)damFamComponent.charatcterRace]) + ((damFamComponent.attackPower*myGamedata.upgradeA_Num_[selectedUnit.UnitUpgradeValue]/100)) + (damFamComponent.attackPower / damFamComponent.DivNum)) * (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f) / selectedUnit.skills[itemC].DivNum;
                    /*DivNum은 다중 공격과 단일 공격에 따른 스킬 데미지를 표시하기 위함*/
                    if(selectedUnit.skills[itemC].DivNum == 1)
                    {
                        updatedDescription = Regex.Replace(updatedDescription, pattern2, (int)D_text + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F1") + ")</color></size>");
                    }
                    else
                    {
                        updatedDescription = Regex.Replace(updatedDescription, pattern2, (int)D_text + " <size=8><color=#F68989>(공격력 x" + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F1") + " / " + selectedUnit.skills[itemC].DivNum + ")</color></size>");
                    }
                    float D_Bufftext = ((upgradeValues[(int)lastChar-49, (int)damFamComponent.charatcterRace]) + ((damFamComponent.attackPower*myGamedata.upgradeA_Num_[selectedUnit.UnitUpgradeValue]/100)) + (damFamComponent.attackPower / damFamComponent.DivNum)) * (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f);

                    /**2*2~*4*4는 버프퍼센트를 표기
                     **는 디버프를 표기*/
                    string pattern4 = @"\*2\*2";
                    updatedDescription = Regex.Replace(updatedDescription, pattern4, (5 + D_Bufftext/100).ToString("F2") + "% <size=8><color=#8989F6>(5% + 공격력의 " + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F2") + "%)</color></size>");
                    string pattern5 = @"\*3\*3";
                    updatedDescription = Regex.Replace(updatedDescription, pattern5, (10 + D_Bufftext/100).ToString("F2") + "% <size=8><color=#8989F6>(10% + 공격력의 " + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F2") + "%)</color></size>");
                    string pattern6 = @"\*4\*4";
                    updatedDescription = Regex.Replace(updatedDescription, pattern6, (15 + D_Bufftext/100).ToString("F2") + "% <size=8><color=#8989F6>(15% + 공격력의 " + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F2") + "%)</color></size>");

                    string pattern3 = @"\*\*";
                    updatedDescription = Regex.Replace(updatedDescription, pattern3, (50 + D_Bufftext/100).ToString("F2") + "% <size=8><color=#8989F6>(50% + 공격력의 " + (selectedUnit.skills[itemC].attackPowers[skillLevel-1] / 100f).ToString("F2") + "%)</color></size>");
                    
                    tooltipDescriptionText.text = updatedDescription;
                }
                else
                {
                    tooltipTitleText.text = selectedUnit.skills[itemC].name;
                    tooltipImage.sprite = selectedUnit.skills[itemC].image;
                    tooltipDescriptionText.text = "Lv. " + (itemC + 1).ToString() + " 에 활성화 됩니다.";
                }
            }
            tooltipPanel.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel.GetComponent<RectTransform>());
        }
    }

    void HideSkillTooltip()
    {
        tooltipPanel.SetActive(false);
        currentlySelectedSkillIndex = null;
    }
}
