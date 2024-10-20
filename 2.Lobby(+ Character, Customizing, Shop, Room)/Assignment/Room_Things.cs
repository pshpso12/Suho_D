using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Room_Things : MonoBehaviour
{
    public GameObject RoomT_object;
    public TMP_Text RoomNumber_T;
    public TMP_InputField RoomTitle_T;
    public TMP_InputField RoomPass_T;
    public Button RoomPassBtn_T;
    public Button RoomExitBtn_T;
    public GameObject RoomReadyBtn_T;
    public Button PReadybtn;
    public GameObject Host_RoomReadyBtn_T;
    public Button HReadybtn;

    [System.Serializable]
    public class PlayerUIElements
    {
        public GameObject Stand_Player;
        public GameObject Canvas_Player;
        public GameObject CanP_Host;
        public GameObject CanP_Ready;
        public GameObject currentCharacter;
        public GameObject Index_Area;
    }
    public PlayerUIElements[] playerUIElements;

    public GameObject contextMenuPrefab;
    public GameObject currentContextMenu;
    public GameObject ContextMenuOption1;
    public GameObject ContextMenuOption2;
    public GameObject ContextMenuOption3;
    public Canvas canvas;
    public GameObject GLoadPanel;
    public Slider GLoadSlider;
    public TMP_Text GLoadingSlider_Text;

    public float Loadingvalue = 0.0f;
    public Coroutine sliderCoroutine;
}
