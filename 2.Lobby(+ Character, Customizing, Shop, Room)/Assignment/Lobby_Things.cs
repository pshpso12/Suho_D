using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Loading_Things : MonoBehaviour
{
    public GameObject chatUI;
    public TMP_InputField chatInputField;
    public TMP_Text InputFieldText;

    public GameObject RoomSetUI;
    public TMP_InputField RoomnameText;
    public TMP_InputField RoompassText;
    public TMP_Dropdown RoompnumText;
    public Button RoomCreateButton;
    public GameObject Room_Cre_Panl;
    public GameObject Room_Enter_Panl;
    public GameObject Room_Enter_Error_Panl;
    public GameObject Room_Enter_Withpass_Panl;
    public TMP_InputField Room_Enter_Withpass_InputField;
    public Button Room_Enter_Withpass_Button;
    public GameObject Room_Enter_Error_Passwrong;
    public GameObject Room_Exit_Forced;
}
