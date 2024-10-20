using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Game_Things : MonoBehaviour
{
    public TMP_Text _Gold_Num;
    public TMP_Text _Gas_Num;

    public Button Buy_Btn;
    public TMP_Text CuUnitCount;
    public Button Change_Btn;
    public Button HL_Btn;
    public Button HD_Btn;
    public Button JL_Btn;
    public Button JD_Btn;

    public TMP_Text _HL_Num;
    public TMP_Text _HD_Num;
    public TMP_Text _JL_Num;
    public TMP_Text _JD_Num;
    public TMP_Text _RoundNum;
    
    [System.Serializable]
    public class PlayerUIElementsGame
    {
        public GameObject UI_Player;
        public TMP_Text UI_Name;
        public TMP_Text UI_Heart;
        public Image UI_Image;
        public Slider UI_Slider;
        public GameObject UI_DisCon;
        public WaveManger Par_Player;
    }
    public PlayerUIElementsGame[] playerUIElementsgame;

    public GameObject chatUI;
    public TMP_InputField chatInputField;
    public TMP_Text InputFieldText;
    public Button Quest_Btn;
    public Button Bag_Btn;
    public Button Set_Btn;
    public GameObject Bag_Paenl;
    public GameObject BagLoot_Image;
    public GameObject tooltipPanel;
    public Image tooltipImage;
    public TMP_Text tooltipNameText;
    public TMP_Text tooltipDesText;
    public Button Bag_Quit_btn;
    public GameObject Quest_Paenl;
    public Button Quest_Quit_btn;
    public GameObject QuestLoot_Image;
    public GameObject EndPanel;
    public GameObject GameEnd_Panel;
    public GameObject Def_;
    public GameObject Vic_;
    public GameObject Result_Panel;
    public bool CuWatch = false;
    public int Total_BC = 100;
    public int Total_UC = 0;
    public GameObject Result_Cha;
    public GameObject Result_Item;
    public GameObject UnderBoard;
    public GameObject ToLobbyPanel;
    public TMP_Text ToLobby_Text;
    public bool ItemResponeDone = false;
    public bool GameResponeDone = false;
    public GameObject _Log_Cons;
    public GameObject _Log_Text;
    public GameObject GStart_;
    public GameObject BossStart_;
    public GameObject UUpgrade_;
    public Coroutine L_Coroutine;

    public Sprite[] GloadList;
}
