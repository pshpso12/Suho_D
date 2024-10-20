using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;

public class GameData_Server : NetworkBehaviour
{
    /*클라이언트가 게임 씬으로 입장한 후 진행*/
    [Command]
    private void CmdSetGameSceneLoaded(NetworkIdentity clientIdentity)
    {
        /*게임서버 입장 후 방 씬에서 게임 씬으로 이동에 사용한 RoomData 스크립트 제거*/
        NetworkServer.UnSpawn(RoomObj);
        NetworkServer.Destroy(RoomObj);
        RealSceneChnage = true;
        Gold = 100;
        StartCoroutine(CheckAllPlayersScene(clientIdentity));
    }
    /*0.01초에 한번 씩 모든 유저가 게임 씬으로 변경했는지를 확인(게임을 동시에 시작하기 위해 0.01로 지정)*/
    private IEnumerator CheckAllPlayersScene(NetworkIdentity clientIdentity)
    {
        while (!AreAllPlayersReady())
        {
            yield return new WaitForSeconds(0.01f);

        RealGameStart(clientIdentity.connectionToClient);
    }
    private bool AreAllPlayersReady()
    {
        Scene thisScene = gameObject.scene;
        var allGameObjects = thisScene.GetRootGameObjects();
        var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<GameData>()).ToList();
        foreach (GameData gameData in allGameData)
        {
            if (gameData == null || !gameData.RealSceneChnage)
            {
                return false;
            }
        }
        return true;
    }
    /*클라이언트는 가스 값을 0으로 변경*/
    [TargetRpc]
    private void RealGameStart(NetworkConnection target)
    {
        SetPlayerGas(0,0);
        StartCoroutine(FadeoutCanvas(10));
    }
    public IEnumerator FadeoutCanvas(float speed)
    {
        audioManager = GameObject.Find("SoundObject").GetComponent<BackgroundManager>();
        /*인게임 사운드와 백그라운드 사운드 재생*/
        if(audioManager != null)
        {
            audioManager.PlayIngameMusic();
            if(!audioManager.IsMusicBackPlaying())
            {
                audioManager.PlayBackMusic();
            }
        }
            
        PanelLoad.gameObject.SetActive(false);
        PanelFade.gameObject.SetActive(true);
        float alpha = PanelFade.color.a;

        /*초기 안내내용 활성화 및 로깅*/
        gamethings.GStart_.SetActive(true);
        ShowMessage("몬스터 수가 <color=#D24444>55</color>를 넘거나 <color=#D24444>보스</color>를 잡지 못하면 패배합니다", 8f);
        ShowMessage(" ", 8f);
        ShowMessage(" ", 8f);

        /*GStart_을 5초 후에 비활성화*/
        StartCoroutine(DeactivateGStartAfterDelay(5f, GStart_));

        while (alpha > 0)
        {
            yield return new WaitForSeconds(0.01f);
            alpha -= 0.01f * speed;
            alpha = Mathf.Max(alpha, 0);
            Color newColor = Color.black;
            newColor.a = alpha;
            PanelFade.color = newColor;

            if (newColor.a <= 0)
            {
                PanelFade.gameObject.SetActive(false);
            }
        }

        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.GVoiceSound(0);
            
        ingameTimer.StartCoroutine(ingameTimer.ServerTimer());
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdSpawnEnemy(opponentIdentity);
    }
    [Command]
    void CmdSpawnEnemy(NetworkIdentity opponentIdentity)
    {
        /*게임 시작 시 첫번째 체력 보스를 정하기 위함*/
        FManager fManager = FindObjectOfType<FManager>();
        if (fManager != null)
        {
            fManager.Debug_11(opponentIdentity);
        }
        
        StartCoroutine(StartRoundTimer());
    }
    /*코루틴으로 라운드 진행*/
    IEnumerator StartRoundTimer()
    {
        yield return new WaitForSeconds(8f);
        
        while (true)
        {
            RoundNum++;
            /*패배한 유저는 적 생성을 안함*/
            if(!F_Death)
            {
                /*라운드에 따른 업적을 위해 체크*/
                CheckForAchievements();
                /*라운드 시작 시 골드 지급 (현재는 모두 동일하게 지급)*/
                AddGoldBasedOnRound(RoundNum);
                if(RoundNum != 9 && RoundNum != 19 && RoundNum != 29 && RoundNum != 39)
                {
                    /*포탈이나 이펙트를 서버에서 관리할 필요가 없다고 판단하여 변수 2개만 보내기로 결정*/
                    /*적이 나오는 포탈을 생성*/
                    RpcCreatePortal(RoundNum, PlayerIndex);
                    yield return new WaitForSeconds(1f);
                    /*적이 나오는 포탈의 이팩트를 생성*/
                    RpcCreateDust(RoundNum, PlayerIndex);
                    yield return new WaitForSeconds(1f);
                    /*라운드에 따른 적 생성*/
                    yield return StartCoroutine(SpawnEnemiesForRound(RoundNum, PlayerIndex));
                    /*포탈 닫음*/
                    RpcDissPortal(RoundNum, PlayerIndex);
                    /*이팩트 닫음*/
                    RpcDustStop(PlayerIndex);
                }
                /*통합 필요*/
                else if(RoundNum == 9)
                {
                    /*보스의 생성 이펙트 진행*/
                    RpcCreateBPar(RoundNum, PlayerIndex);
                    yield return new WaitForSeconds(7f);
                    BossNum++;
                    /*보스 생성*/
                    SpawnBoss_EE();
                    yield return new WaitForSeconds(0.5f);
                    /*보스 생성 추가 이펙트*/
                    RpcDissBPar(RoundNum, PlayerIndex);
                }
                else if(RoundNum == 19)
                {
                    RpcCreateBPar(RoundNum, PlayerIndex);
                    yield return new WaitForSeconds(7f);
                    BossNum++;
                    SpawnBoss_EE();
                    yield return new WaitForSeconds(0.5f);
                    RpcDissBPar(RoundNum, PlayerIndex);
                }
                else if(RoundNum == 29)
                {
                    RpcCreateBPar(RoundNum, PlayerIndex);
                    yield return new WaitForSeconds(7f);
                    BossNum++;
                    SpawnBoss_EE();
                    yield return new WaitForSeconds(0.5f);
                    RpcDissBPar(RoundNum, PlayerIndex);
                }
                else if(RoundNum == 39)
                {
                    RpcCreateBPar(RoundNum, PlayerIndex);
                    yield return new WaitForSeconds(7f);
                    BossNum++;
                    SpawnBoss_EE();
                    yield return new WaitForSeconds(0.5f);
                    RpcDissBPar(RoundNum, PlayerIndex);
                    /*40라운드가 시작하고 45초가 지나면 승리*/
                    yield return new WaitForSeconds(45f);
                    if(!F_Death)
                        Vic_Game();
                }
            }
            else
            {
                /*패배한 유저도 현재 몇 라운드인지를 알게하기 위해 적용*/
                if(RoundNum != 9 && RoundNum != 19 && RoundNum != 29 && RoundNum != 39)
                {
                    yield return new WaitForSeconds(1f);
                    yield return new WaitForSeconds(1f);
                    yield return StartCoroutine(SpawnEnemiesForRound(RoundNum, PlayerIndex));
                }
                else if(RoundNum == 9)
                {
                    yield return new WaitForSeconds(7f);
                    yield return new WaitForSeconds(0.5f);
                }
                else if(RoundNum == 19)
                {
                    yield return new WaitForSeconds(7f);
                    yield return new WaitForSeconds(0.5f);
                }
                else if(RoundNum == 29)
                {
                    yield return new WaitForSeconds(7f);
                    yield return new WaitForSeconds(0.5f);
                }
                else if(RoundNum == 39)
                {
                    yield return new WaitForSeconds(7f);
                    yield return new WaitForSeconds(0.5f);
                }
            }

            yield return new WaitForSeconds(15f);
        }
    }
    private void AddGoldBasedOnRound(int RoundNum)
    {
        if (RoundNum > 0 && RoundNum < 9)
        {
            AddGold(65);
        }
        else if (RoundNum == 9)
        {
            AddGold(65);
        }
        else if (RoundNum > 9 && RoundNum < 19)
        {
            AddGold(65);
        }
        else if (RoundNum == 19)
        {
            AddGold(65);
        }
        else if (RoundNum > 19 && RoundNum < 29)
        {
            AddGold(65);
        }
        else if (RoundNum == 29)
        {
            AddGold(65);
        }
        else if (RoundNum > 29 && RoundNum < 39)
        {
            AddGold(65);
        }
        else if (RoundNum == 39)
        {
           AddGold(65);
        }
    }
    /*라운드에 맞춰서 맞는 적을 생성*/
    private IEnumerator SpawnEnemiesForRound(int roundNumber, int PIndex)
    {
        for (int i = 0; i < 35; i++)
        {
            if(!F_Death)
            {
                GameObject enemyObject = Instantiate(enemyPrefab[roundNumber].gameObject, spawnPoint.position, spawnPoint.rotation);
                enemy enemyScript = enemyObject.GetComponent<enemy>();

                if (enemyScript != null)
                {
                    Scene thisScene = gameObject.scene;
                    SceneManager.MoveGameObjectToScene(enemyObject, thisScene);
                    enemyScript.EnemyInitialize(PlayerIndex, this);
                    NetworkServer.Spawn(enemyObject, connectionToClient);
                    ECount(PIndex);
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }
    /*라운드에 맞는 보스 생성*/
    void SpawnBoss_EE()
    {
        GameObject BossObject = Instantiate(BossPrefab[BossNum].gameObject, spawnPoint.position, spawnPoint.rotation);
        Boss bossScript = BossObject.GetComponent<Boss>();
        if (bossScript != null)
        {
            Scene thisScene = gameObject.scene;
            SceneManager.MoveGameObjectToScene(BossObject, thisScene);
            bossScript.EnemyInitialize(PlayerIndex, this);
            NetworkServer.Spawn(BossObject, connectionToClient);
            bossScript.RpcHPBar(connectionToClient);
            BossDoneLog("BOSS가 한 바퀴를 돌기 전에 처치하지 못하면 <color=#D24444>패배</color>합니다!", true);
        }
    }
    /*적의 개수 증가*/
    void ECount(int PIndex)
    {
        Scene thisScene = gameObject.scene;
        var rootObjects = thisScene.GetRootGameObjects();
        foreach (GameObject rootObj in rootObjects)
        {
            if (rootObj.CompareTag("GameData"))
            {
                GameData gameData = rootObj.GetComponent<GameData>();
                if (gameData != null)
                {
                    gameData.IncrementEnemyCount(PIndex);
                }
            }
        }
    }
    /*적 개수 하락 Enemy()에서 적이 죽으면 진행*/
    public void ECountDo(int PIndex)
    {
        Scene thisScene = gameObject.scene;
        var rootObjects = thisScene.GetRootGameObjects();
        foreach (GameObject rootObj in rootObjects)
        {
            if (rootObj.CompareTag("GameData"))
            {
                GameData gameData = rootObj.GetComponent<GameData>();
                if (gameData != null)
                {
                    gameData.DecrementEnemyCount(PIndex);
                }
            }
        }
    }
    /*각 유저의 적 개수를 저장하고 업적과 적이 56이 되면 패배 설정*/
    public void IncrementEnemyCount(int playerIndex)
    {
        if (EnemyCounts.ContainsKey(playerIndex))
        {
            EnemyCounts[playerIndex]++;
        }
        else
        {
            EnemyCounts[playerIndex] = 1;
        }

        if(playerIndex == this.PlayerIndex)
        {
            CheckForAchievements();
            if(EnemyCounts[playerIndex] > 55f && !F_Death)
            {
                F_Death = true;
                Def_Game();
            }   
        }
    }
    /*적 개수 감소 저장 및 업적체크*/
    public void DecrementEnemyCount(int playerIndex)
    {
        if (EnemyCounts.ContainsKey(playerIndex) && EnemyCounts[playerIndex] > 0)
        {
            EnemyCounts[playerIndex]--;
        }
        if(playerIndex == this.PlayerIndex)
        {
            CheckForAchievements();
        }
    }
    /*클라이언트 측에서 이펙트 진행 다른 클라이언트도 진행*/
    [ClientRpc]
    void RpcCreateDust(int roundNumber, int PIndex)
    {   
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");

        foreach (GameObject gameDataObj in gameDataObjects)
        {
            GameData gameData = gameDataObj.GetComponent<GameData>();
            if (gameData != null && gameData.GetComponent<NetworkIdentity>().isOwned)
            {
                gameData.CreateDust(roundNumber, PIndex);
            }
        }
    }
    /*10라운드 별로 다른 이펙트*/
    void CreateDust(int roundNumber, int PIndex)
    {
        if(roundNumber < 9)
            playerUIElementsgame[PIndex].Par_Player.Dust1P();
        else if(roundNumber < 19)
            playerUIElementsgame[PIndex].Par_Player.Dust2P();
        else if(roundNumber < 29)
            playerUIElementsgame[PIndex].Par_Player.Dust3P();
        else if(roundNumber < 39)
            playerUIElementsgame[PIndex].Par_Player.Dust4P();
    }
    /*클라이언트 측에서 포탈 생성 다른 클라이언트도 진행*/
    [ClientRpc]
    void RpcCreatePortal(int roundNumber, int PIndex)
    {   
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");

        foreach (GameObject gameDataObj in gameDataObjects)
        {
            GameData gameData = gameDataObj.GetComponent<GameData>();
            if (gameData != null && gameData.GetComponent<NetworkIdentity>().isOwned)
            {
                gameData.CreatePortal(roundNumber, PIndex);
            }
        }
    }
    /*10라운드 별로 다른 포탈*/
    void CreatePortal(int roundNumber, int PIndex)
    {
        if(roundNumber < 9)
            gamethings.playerUIElementsgame[PIndex].Par_Player.ADust1P();
        else if(roundNumber < 19)
            gamethings.playerUIElementsgame[PIndex].Par_Player.ADust2P();
        else if(roundNumber < 29)
            gamethings.playerUIElementsgame[PIndex].Par_Player.ADust3P();
        else if(roundNumber < 39)
            gamethings.playerUIElementsgame[PIndex].Par_Player.ADust4P();
    }
    /*클라이언트 측에서 포탈 제거 다른 클라이언트도 진행*/
    [ClientRpc]
    void RpcDissPortal(int roundNumber, int PIndex)
    {   
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");

        foreach (GameObject gameDataObj in gameDataObjects)
        {
            GameData gameData = gameDataObj.GetComponent<GameData>();
            if (gameData != null && gameData.GetComponent<NetworkIdentity>().isOwned)
            {
                gameData.DissPortal(roundNumber, PIndex);
            }
        }
    }
    /*10라운드 별로 다른 포탈*/
    void DissPortal(int roundNumber, int PIndex)
    {
        if(roundNumber < 9)
            gamethings.playerUIElementsgame[PIndex].Par_Player.CDust1P();
        else if(roundNumber < 19)
            gamethings.playerUIElementsgame[PIndex].Par_Player.CDust2P();
        else if(roundNumber < 29)
            gamethings.playerUIElementsgame[PIndex].Par_Player.CDust3P();
        else if(roundNumber < 39)
            gamethings.playerUIElementsgame[PIndex].Par_Player.CDust4P();
    }
    /*10라운드 별로 다른 이펙트 종료*/
    [ClientRpc]
    void RpcDustStop(int PIndex)
    {
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");

        foreach (GameObject gameDataObj in gameDataObjects)
        {
            GameData gameData = gameDataObj.GetComponent<GameData>();
            if (gameData != null && gameData.GetComponent<NetworkIdentity>().isOwned)
            {
                gameData.DustStop(PIndex);
            }
        }
    }
    void DustStop(int PIndex)
    {
        gamethings.playerUIElementsgame[PIndex].Par_Player.DustStop();
    }
    /*보스 이펙트 진행 다른 클라이언트도 진행*/
    [ClientRpc]
    void RpcCreateBPar(int roundNumber, int PIndex)
    {   
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");

        foreach (GameObject gameDataObj in gameDataObjects)
        {
            GameData gameData = gameDataObj.GetComponent<GameData>();
            if (gameData != null && gameData.GetComponent<NetworkIdentity>().isOwned)
            {
                gameData.CreateBPar(roundNumber, PIndex);
            }
        }
    }
    /*보스 별 다른 이펙트 진행*/
    void CreateBPar(int roundNumber, int PIndex)
    {
        if(roundNumber == 9)
            gamethings.playerUIElementsgame[PIndex].Par_Player.B1par();
        else if(roundNumber == 19)
            gamethings.playerUIElementsgame[PIndex].Par_Player.B2par();
        else if(roundNumber == 29)
            gamethings.playerUIElementsgame[PIndex].Par_Player.B3par();
        else if(roundNumber == 39)
            gamethings.playerUIElementsgame[PIndex].Par_Player.B4par();
    }
    /*보스 추가 이펙트 진행 다른 클라이언트도 진행*/
    [ClientRpc]
    void RpcDissBPar(int roundNumber, int PIndex)
    {   
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");

        foreach (GameObject gameDataObj in gameDataObjects)
        {
            GameData gameData = gameDataObj.GetComponent<GameData>();
            if (gameData != null && gameData.GetComponent<NetworkIdentity>().isOwned)
            {
                gameData.DissBPar(roundNumber, PIndex);
            }
        }
    }
    /*보스 별 다른 이펙트 진행*/
    void DissBPar(int roundNumber, int PIndex)
    {
        if(roundNumber == 9)
            gamethings.playerUIElementsgame[PIndex].Par_Player.D1par();
        else if(roundNumber == 19)
            gamethings.playerUIElementsgame[PIndex].Par_Player.D2par();
        else if(roundNumber == 29)
            gamethings.playerUIElementsgame[PIndex].Par_Player.D3par();
        else if(roundNumber == 39)
            gamethings.playerUIElementsgame[PIndex].Par_Player.D4par();
    }
    /*클라이언트 접속이 종료될 때 게임서버에 실행*/
    public void HandleDisconnection()
    {
        Scene targetScene = gameObject.scene;
        foreach(GameObject obj in targetScene.GetRootGameObjects())
        {
            GameData gameData = obj.GetComponentInChildren<GameData>(true);
            if (gameData != null && gameData != this)
            {
                GameItem MyItem = Gameinventory[this.PlayerIndex];
                gameData.Gameinventory[this.PlayerIndex] = new GameItem
                {
                    PlayerExist = MyItem.PlayerExist,
                    FakeHost = MyItem.FakeHost,
                    PlayerNickname = MyItem.PlayerNickname,
                    PlayerLevel = MyItem.PlayerLevel,
                    PlayerTexture = MyItem.PlayerTexture,
                    PlayerMainCharacterID = MyItem.PlayerMainCharacterID,
                    PlayerTopOutfitID = MyItem.PlayerTopOutfitID,
                    PlayerBottomOutfitID = MyItem.PlayerBottomOutfitID,
                    PlayerShoesOutfitID = MyItem.PlayerShoesOutfitID,
                    PConnection = false
                };
            }
        }
        /*FManager에서 협동보스의 체력을 현재 인원에 맞춰 하락*/
        FManager fManager = FindObjectOfType<FManager>();
        if (fManager != null)
        {
            fManager.DissDownHp();
        }

        /*마스터서버로 접속이 끊김을 보내서 방에서 해당 유저를 삭제하거나, 혼자인 경우 방을 삭제함*/
        var GameRegi = FindObjectOfType<Insight.GameRegistration_Kihana>();
        if (GameRegi != null)
        {
            GameRegi.SendPlayerDisConnect(this.Gameinventory[this.PlayerIndex].PlayerNickname, this.PlayerIndex, LeaveState, ToLobby);
        }
    }
    /**FManager의 DissDownHP()**/
    public void DissDownHp()
    {
        GameObject[] gameDataObjects = GameObject.FindGameObjectsWithTag("GameData");
        if(newObj)
        {
            CBoss cbossCom = newObj.GetComponent<CBoss>();
            if (cbossCom)
            {
                /*3명에서 시작해서 한명이 접속을 종료한 경우 현재체력, 최대체력을 3/4을 곱함*/
                float reductionFactor = (float)(gameDataObjects.Length) / (gameDataObjects.Length + 1);
                cbossCom.UpdateDissNewStaminaUI(reductionFactor);
            }
        }
    }
    [Command]
    private void CmdGetMC(NetworkIdentity clientIdentity)
    {
        Scene currentScene = gameObject.scene;
        GameObject[] rootObjects = currentScene.GetRootGameObjects();
        GameObject upgradeFamObject = rootObjects.FirstOrDefault(obj => obj.name == "M_cc_ex");
        if (upgradeFamObject != null)
        {
            /*종족 강화 값 저장*/
            upgrade_fam = upgradeFamObject.GetComponent<Upgrade_Fam>();
            /*몬스터 프리팹, 유저 위치 별 몬스터와 캐릭터 스폰 위치*/
            enemyPrefab = upgradeFamObject.GetComponent<MonsterList>().enemyPrefab;
            BossPrefab = upgradeFamObject.GetComponent<MonsterList>().BossPrefab;
            spawnPoint = upgradeFamObject.GetComponent<MonsterList>().spawnPoint[PlayerIndex];
            CharaterspawnPoint = GameObject.Find("Player" + (PlayerIndex+1) +"_P");
        }
    }
    [Command]
    private void CmdGetAchievement(NetworkIdentity clientIdentity)
    {
        /*라운드 업적 2~35 라운드 중 하나를 지정해서 해당 라운드 도달 시 업적 완료*/
        achievements.Add(new Achievement(0, AchievementType.Round, UnityEngine.Random.Range(2, 35), 0, null, null, AchievementCompleted));
        Achievementinventory.Add(new Achievement_Sh(0, 0, achievements[0].Mission_Name, achievements[0].Mission_Description, achievements[0].Mission_Reward, false));
        /*5개중 랜덤으로 지정된 채팅을 입력하면 업적 완료*/
        int randomIndex = UnityEngine.Random.Range(0, 5); 
        achievements.Add(new Achievement(0, AchievementType.Chat, 0, 0, CchatList[randomIndex], null, AchievementCompleted));
        Achievementinventory.Add(new Achievement_Sh(1, 0, achievements[1].Mission_Name, achievements[1].Mission_Description, achievements[1].Mission_Reward, false));
        /*35~54 중 지정된 체력에 도달 시 업적 완료*/
        achievements.Add(new Achievement(0, AchievementType.Life, UnityEngine.Random.Range(35, 54), 0, null, null, AchievementCompleted));
        Achievementinventory.Add(new Achievement_Sh(2, 0, achievements[2].Mission_Name, achievements[2].Mission_Description, achievements[2].Mission_Reward, false));
        /*10개의 유닛 중 2개의 유닛을 지정해서 두 유닛을 보유 시 업적 완료*/
        List<string> randomUnitsM = GetRandomElements(UNameList.GetRange(0, 10), 2);
        achievements.Add(new Achievement(0, AchievementType.Units_M, 0, 0, null, randomUnitsM, AchievementCompleted));
        Achievementinventory.Add(new Achievement_Sh(3, 0, achievements[3].Mission_Name, achievements[3].Mission_Description, achievements[3].Mission_Reward, false));
        
        var achievementTypes = new List<AchievementType>
        {
            AchievementType.Units_Starter,
            AchievementType.Units_711,
            AchievementType.Coin,
            AchievementType.Units_L8,
            AchievementType.Units_D8,
            AchievementType.Survive_D,
            AchievementType.Units_SameE4,
            AchievementType.All_Kill,
            AchievementType.All_Kill2,
            AchievementType.Units_FSell
        };
        /*10개의 업적 중 5개를 선정*/
        var selectedTypes = GetRandomElements(achievementTypes, 5);

        int a = 4;
        foreach (var type in selectedTypes)
        {
            switch (type)
            {
                case AchievementType.Units_Starter:
                    /*서로 다른 유닛을 종류 별로 1개씩 4개 이상 보유 시*/
                    achievements.Add(new Achievement(1, AchievementType.Units_Starter, 0, 0, null, UNameList.GetRange(0, 10), AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Units_711:
                    /*버프 유닛 두 개 보유 시*/
                    achievements.Add(new Achievement(1, AchievementType.Units_711, 0, 0, null, UNameList.GetRange(8, 2), AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Coin:
                    /*보유 골드가 1,000이상 일 때*/
                    achievements.Add(new Achievement(1, AchievementType.Coin, 1000, 0, null, null, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Units_L8:
                    /*Light 종족의 유닛 4개 보유 시*/
                    List<string> oddUnits = FilterUnits(UNameList, index => index % 2 != 0);
                    achievements.Add(new Achievement(1, AchievementType.Units_L8, 0, 0, null, oddUnits, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Units_D8:
                    /*Dark 종족의 유닛 4개 보유 시*/
                    List<string> evenUnits = FilterUnits(UNameList, index => index % 2 == 0);
                    achievements.Add(new Achievement(1, AchievementType.Units_D8, 0, 0, null, evenUnits, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Survive_D:
                    /*적이 54 이상 일 때*/
                    achievements.Add(new Achievement(1, AchievementType.Survive_D, 54, 0, null, null, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Units_SameE4:
                    /*동일한 유닛 3개 보유 시*/
                    achievements.Add(new Achievement(1, AchievementType.Units_SameE4, 0, 0, null, UNameList.GetRange(0, 10), AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.All_Kill:
                    /*20라운드에 존재하는 적이 없을 때*/
                    achievements.Add(new Achievement(1, AchievementType.All_Kill, 20, 0, null, null, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.All_Kill2:
                    /*30라운드에 존재하는 적이 없을 때*/
                    achievements.Add(new Achievement(1, AchievementType.All_Kill2, 30, 0, null, null, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
                case AchievementType.Units_FSell:
                    /*유닛을 처음 판매했을 때*/
                    achievements.Add(new Achievement(1, AchievementType.Units_FSell, 0, 0, null, null, AchievementCompleted));
                    Achievementinventory.Add(new Achievement_Sh(a, 1, achievements[a].Mission_Name, achievements[a].Mission_Description, achievements[a].Mission_Reward, false));
                    break;
            }
            a++;
        }
    }
    /*중복 없는 랜덤 반환을 위해서 사용*/
    List<string> GetRandomElements(List<string> list, int numberOfElements)
    {
        List<string> tempList = new List<string>(list);
        List<string> selectedElements = new List<string>();
        System.Random random = new System.Random();

        for (int i = 0; i < numberOfElements; i++)
        {
            if (tempList.Count > 0)
            {
                int index = random.Next(tempList.Count);
                selectedElements.Add(tempList[index]);
                tempList.RemoveAt(index);
            }
        }

        return selectedElements;
    }
    /*중복 없는 랜덤 반환을 위해서 사용*/
    private List<AchievementType> GetRandomElements(List<AchievementType> list, int numberOfElements)
    {
        var tempList = new List<AchievementType>(list);
        var selectedElements = new List<AchievementType>();
        System.Random random = new System.Random();

        for (int i = 0; i < numberOfElements; i++)
        {
            if (tempList.Count > 0)
            {
                int index = random.Next(tempList.Count);
                selectedElements.Add(tempList[index]);
                tempList.RemoveAt(index);
            }
        }

        return selectedElements;
    }
    /*0포함 짝수, 홀수 리스트를 반환*/
    List<string> FilterUnits(List<string> unitList, Func<int, bool> predicate)
    {
        List<string> filteredUnits = new List<string>();
        for (int i = 0; i < unitList.Count; i++)
        {
            if (predicate(i))
            {
                filteredUnits.Add(unitList[i]);
            }
        }
        return filteredUnits;
    }
    /*업적 완료 확인을 위해 실행*/
    void CheckForAchievements()
    {
        List<string> unitNames = new List<string>();
        foreach (GameObject character in characterPools)
        {
            Dam_Fam damFamComponent = character.GetComponent<Dam_Fam>();
            if (damFamComponent != null)
            {
                unitNames.Add(damFamComponent.Unit_name);
            }
        }

        int currentRound = RoundNum + 1;
        string currentChat = Last_message;
        int currentLife = 0;
        if(EnemyCounts.ContainsKey(this.PlayerIndex))
        {
            currentLife = EnemyCounts[this.PlayerIndex];
        }
        int currentCoin = Gas;
        bool currentState = F_Sell;

        foreach(Achievement achievement in achievements)
        {
            achievement.CheckAchievement(currentRound, currentChat, currentLife, unitNames, currentCoin, currentState);
        }
    }
    /*업적 별 보상*/
    void AchievementCompleted(Achievement achievement)
    {
        int index = achievements.IndexOf(achievement);
        if (index != -1 && index < Achievementinventory.Count)
        {
            Achievement_Sh MyAchievementItem = Achievementinventory[index];
            Achievementinventory[index] = new Achievement_Sh
            {
                Mission_Num = MyAchievementItem.Mission_Num,
                Mission_Type = MyAchievementItem.Mission_Type,
                Mission_Name = MyAchievementItem.Mission_Name,
                Mission_Description = MyAchievementItem.Mission_Description,
                Mission_Reward = MyAchievementItem.Mission_Reward,
                achieved = true
            };
        }
        switch (achievement.Type)
        {
            case AchievementType.Round:
            case AchievementType.Life:
            case AchievementType.Chat:
            case AchievementType.Units_M:
                AddGold(100);
                achievement.achieved = true;
                break;
            case AchievementType.Units_R:
            case AchievementType.Units_L8:
            case AchievementType.Units_D8:
                AddGold(300);
                achievement.achieved = true;
                break;
            case AchievementType.Units_U:
            case AchievementType.Units_GreenGreen:
                AddGold(400);
                achievement.achieved = true;
                break;
            case AchievementType.Units_E:
            case AchievementType.Units_SameE4:
            case AchievementType.All_Kill:
            case AchievementType.Units_Starter:
                AddGold(150);
                achievement.achieved = true;
                break;
            case AchievementType.Units_711:
                AddGas(200);
                achievement.achieved = true;
                break;
            case AchievementType.Coin:
                AddGas(300);
                achievement.achieved = true;
                break;
            case AchievementType.Survive_D:
            case AchievementType.All_Kill2:
                AddGas(200);
                achievement.achieved = true;
                break;
            case AchievementType.Units_FSell:
                AddGold(50);
                achievement.achieved = true;
                break;
            default:
                break;
        }
    }
    /*종족 업그레이드 버튼 비활성화, 가스가 업그레이드 값 + 1 보다 높을 때 활성화*/
    [Command]
    void UpdateUpgradeButtonState(NetworkIdentity clientIdentity, int playerID, int index)
    {
        int currentUpgradeValue = (int)upgrade_fam.upgradeValues[playerID, index];
        if (GetGas() < currentUpgradeValue + 1)
        {
            RpcupgradeF(clientIdentity.connectionToClient, index);
        }
        else
        {
            RpcupgradeT(clientIdentity.connectionToClient, index);
        }
    }
    [TargetRpc]
    void RpcupgradeT(NetworkConnection target, int index)
    {
        Button upgradeButton = GetUpgradeButtonComponent(index);
        upgradeButton.interactable = true;
    }
    [TargetRpc]
    void RpcupgradeF(NetworkConnection target, int index)
    {
        Button upgradeButton = GetUpgradeButtonComponent(index);
        upgradeButton.interactable = false;
    }
    /*골드 추가 누적 골드를 게임 종료 시 보여주기 위해 Total_BC 사용*/
    public void AddGold(int amount)
    {
        Gold += amount;
        Total_BC += amount;
    }
    public void SubtractGold(int amount)
    {
        Gold -= amount;
    }
    /*가스 추가 누적 가스를 게임 종료 시 보여주기 위해 Total_UC 사용*/
    public void AddGas(int amount)
    {
        Gas += amount;
        Total_UC += amount;
    }
    public void SubtractGas(int amount)
    {
        Gas -= amount;
    }
    /*구매 버튼 클릭 시 골드가 100 이상 일 때 유닛 생성 진행*/
    [Command]
    void CmdBuy_listener(NetworkIdentity clientIdentity)
    {
        float currentGold = Gold;
        if(currentGold >= 100)
        {
            InitializeCharater();
        }
        else
        {
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            RpcNoGas(clientIdentity.connectionToClient);
        }
    }
    /*캐릭터 생성*/
    void InitializeCharater()
    {
        /*보유 가능 유닛은 4, 협동 보스 클리어 시 5*/
        int MaxCha = 4;
        if(CBossUnit)
            MaxCha += 1;
        if(characterPools.Count < MaxCha )
        {
            /*스폰할 위치를 확인, */
            Vector3? spawnPosition = FindValidPosition();
            if (spawnPosition != null)
            {
                /*골드를 차감하고 랜덤한 캐릭터를 생성*/
                SubtractGold(100);
                int Rindex = UnityEngine.Random.Range(0, 10);
                GameObject character = Instantiate(GamecharacterPrefabs[Rindex], spawnPosition.Value, Quaternion.Euler(0, 180, 0));
                Scene thisScene = gameObject.scene;
                SceneManager.MoveGameObjectToScene(character, thisScene);
                characterPools.Add(character);
                CheckForAchievements();

                NetworkServer.Spawn(character, connectionToClient);
                /*초기 유닛의 값들 초기화*/
                UnitManager_Net unitManager = character.GetComponent<UnitManager_Net>();
                if (unitManager != null)
                {
                    unitManager.UnitLevel = 1;
                    unitManager.UnitCurrentExp = 0;
                    unitManager.UnitMaxExp = 50;
                    unitManager.UnitUpgradeValue = 0;
                    unitManager.Buff_Power = 0f;
                    unitManager.Buff_Speed = 0f;

                    unitManager.SpwanEffect();
                }
                /*유저의 캐릭터 의상을 적용*/
                CharacterCostumeInfo costume = characterDataList[Rindex];
                ApplyCostumeToCharacter(costume, character);
                /*유저의 캐릭터 지정, 자기 유닛만 클릭 가능하게 함*/
                character.tag = "Unit_p" + (PlayerIndex + 1);
                ApplyTag(character, PlayerIndex);
                AddCuUnits(connectionToClient, character);
                RpcSpawnDone(connectionToClient);
            }
            else
            {
                RpcNeedPlace(connectionToClient);
            }
        }
        else
        {
            RpcNeedPlace(connectionToClient);
        }   
    }
    /*생성 가능 위치 찾기*/
    Vector3? FindValidPosition()
    {
        int maxAttempts = 100;
        int attempts = 0;
        /*기존 스폰 위치에 y축으로 대략 -1 한 것을 centerpostion으로 지정*/
        Vector3 centerPosition = CharaterspawnPoint.transform.TransformPoint(new Vector3(0f, -0.972502112f, 0f));
        /*해당 위치가 가능하면 중앙을 우선 지정*/
        if (IsPositionValid(centerPosition))
        {
            return centerPosition;
        }
        /*가능 위치를 100회 진행, 범위는 최대 반지름 4로 지정하고, 2파이로 지정하여 360도로 지정*/
        while (attempts < maxAttempts)
        {
            float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
            float radius = UnityEngine.Random.Range(0, 4);
            Vector3 position = CharaterspawnPoint.transform.TransformPoint(new Vector3(radius * Mathf.Cos(angle), -0.972502112f, radius * Mathf.Sin(angle)));
            if (IsPositionValid(position))
            {
                return position;
            }
            attempts++;
        }
        return null;
    }
    /*현재 유닛과의 거리를 비교하며 거리가 2보다 낮으면 겹치는 것으로 간주하고 false 반환*/
    bool IsPositionValid(Vector3 position)
    {
        if (characterPools != null)
        {
            foreach (GameObject character in characterPools)
            {
                if (Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(character.transform.position.x, 0, character.transform.position.z)) < 2.0f)
                {
                    return false;
                }
            }
        }
        return true;
    }
    /*유닛 생성 사운드 재생*/
    [TargetRpc]
    public void RpcSpawnDone(NetworkConnection target)
    {
        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.USpawnSound();
    }
    /*자리가 없음을 알림*/
    [TargetRpc]
    public void RpcNeedPlace(NetworkConnection target)
    {
        ShowMessage("생성 공간이 부족합니다. 자리를 비워주세요.", 3f);
        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.NotEnoughtSound();
    }
    /*구매 버튼 위의 현재 유닛 정보를 입력*/
    [TargetRpc]
    public void AddCuUnits(NetworkConnection target, GameObject character)
    {
        if (character != null)
        {
            Globals_Net.CurrentExist_UNITS.Add(character);
            if(CBossUnit)
            {
                CuUnitCount.text = Globals_Net.CurrentExist_UNITS.Count + " / 5";
            }
            else
            {
                CuUnitCount.text = Globals_Net.CurrentExist_UNITS.Count + " / 4";
            }
            
        }
    }
    /*협동 보스 클리어 시 서버측에서 진행 (강화 확률 5% 증가)*/
    public void CBossUpgradeDone()
    {
        CBossUpgrade = true;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        RpcCBossUpgradeDone(opponentIdentity.connectionToClient);
    }
    /*클라이언트 측에서도 강화 툴팁에서 확인 가능하게 하기 위함*/
    [TargetRpc]
    public void RpcCBossUpgradeDone(NetworkConnection target)
    {
        selectchange.CBossUpgrade = true;
    }
    /*협동 보스 클리어 시 서버 측에서 진행(보유 가능 유닛 5로 변경)*/
    public void CBossUnitDone()
    {
        CBossUnit = true;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        RpcCBossUnitDone(opponentIdentity.connectionToClient);
    }
    /*클리어 후 바로 구매 가능함을 반영하기 위함*/
    [TargetRpc]
    public void RpcCBossUnitDone(NetworkConnection target)
    {
        CBossUnit = true;
        CuUnitCount.text = Globals_Net.CurrentExist_UNITS.Count + " / 5";
        if (GetGold() < 100)
        {
            Buy_Btn.interactable = false;
        }
        else
        {
            if(CBossUnit)
            {
                if(Globals_Net.CurrentExist_UNITS.Count < 5)
                    Buy_Btn.interactable = true;
                else if(Globals_Net.CurrentExist_UNITS.Count >= 5)
                    Buy_Btn.interactable = false;
            }
            else
            {
                if(Globals_Net.CurrentExist_UNITS.Count < 4)
                    Buy_Btn.interactable = true;
                else if(Globals_Net.CurrentExist_UNITS.Count >= 4)
                    Buy_Btn.interactable = false;   
            }
        }
    }
    /*유닛 일반강화 클릭 시*/
    [Command]
    void Unit_Upgrade(GameObject Up1_Object)
    {
        if(Up1_Object)
        {
            Dam_Fam selectedDamFam = Up1_Object.GetComponent<Dam_Fam>();
            UnitManager_Net selectedManager = Up1_Object.GetComponent<UnitManager_Net>();
            if (selectedManager.UnitUpgradeValue < upgradeCosts_.Length)
            {
                /*필요 골드*/
                int requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue];
                /*강화 확률*/
                int successProbability = upgradeProbabilities_[selectedManager.UnitUpgradeValue];
                /*협동보스 클리어 시 확률 5% 증가*/
                if(CBossUpgrade)
                    successProbability += 5;
                float currentGold = Gold;
                /*보유 골드가 필요 골드보다 많을 때*/
                if(currentGold >= requiredGoldForUpgrade)
                {
                    /*골드 차감 후 1,100 중 랜덤한 값을 골라 이 값이 확률 값 보다 작은지를 확인하는 방식*/
                    SubtractGold(requiredGoldForUpgrade);
                    int randomValue = UnityEngine.Random.Range(1, 101);
                    if(randomValue <= successProbability)
                    {
                        selectedManager.UnitUpgradeValue++;
                        /*20강 도달 시 컷신을 생성하기 위함*/
                        if(selectedManager.UnitUpgradeValue == 20)
                            RpcUpgradeDone(true, Up1_Object, true);
                        else
                            RpcUpgradeDone(true, Up1_Object, false);

                        RpcUpgradeDoneLog(connectionToClient, "신비로운 힘으로 강화에 <color=#FFFF92>성공</color>하였습니다!", 0);
                    }
                    else
                    {
                        /*10강 이하는 실패해도 강화 유지*/
                        if(selectedManager.UnitUpgradeValue < 10)
                        {
                            selectedManager.UnitUpgradeValue = selectedManager.UnitUpgradeValue;
                            RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 유지)</color>", 1);
                        }
                        /*10강 이하는 실패 시 다시 10프로 확률로 강화 하락*/
                        else if(selectedManager.UnitUpgradeValue < 15)
                        {
                            int randomDownValue = UnityEngine.Random.Range(1, 101 - successProbability);
                            if(randomDownValue <= 10)
                            {
                                selectedManager.UnitUpgradeValue--;
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 -1)</color>", 2);
                            }
                            else
                            {
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 유지)</color>", 1);
                            }
                        }
                        /*20강 이하는 실패 시 다시 15프로 확률로 강화 하락*/
                        else if(selectedManager.UnitUpgradeValue < 20)
                        {
                            int randomDownValue = UnityEngine.Random.Range(1, 101 - successProbability);
                            if(randomDownValue <= 15)
                            {
                                selectedManager.UnitUpgradeValue--;
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 -1)</color>", 2);
                            }
                            else
                            {
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 유지)</color>", 1);
                            } 
                        }
                        /*25강 이하는 실패 시 다시 20프로 확률로 강화 하락, 5프로 확률로 파괴*/
                        else if(selectedManager.UnitUpgradeValue < 25)
                        {
                            int randomDownValue = UnityEngine.Random.Range(1, 101 - successProbability);
                            if(randomDownValue <= 20)
                            {
                                selectedManager.UnitUpgradeValue--;
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 -1)</color>", 2);
                            }
                            else if(randomDownValue <= 25)
                            {
                                selectedManager.UnitUpgradeValue = 0;
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다...</color> <color=#D24444>(강화 파괴!!)</color>", 3);
                            }
                            else
                            {
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 유지)</color>", 1);
                            }
                        }
                        /*30강 이하는 실패 시 다시 25프로 확률로 강화 하락, 10프로 확률로 파괴*/
                        else
                        {
                            int randomDownValue = UnityEngine.Random.Range(1, 101 - successProbability);
                            if(randomDownValue <= 25)
                            {
                                selectedManager.UnitUpgradeValue--;
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 -1)</color>", 2);
                            }
                            else if(randomDownValue <= 35)
                            {
                                selectedManager.UnitUpgradeValue = 0;
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다...</color> <color=#D24444>(강화 파괴!!)</color>", 3);
                            }
                            else
                            {
                                RpcUpgradeDoneLog(connectionToClient, "<color=#E0E0E0>강화에 실패하였습니다... (강화 유지)</color>", 1);
                            }
                        }
                        RpcUpgradeDone(false, Up1_Object, false);
                    }
                }
                else
                {
                    RpcNotHaveGold(connectionToClient);
                }
            }
            else
            {

            }
        }
        else
        {
            RpcNotHaveUnit(connectionToClient);
        }
    }
    /*보호강화 버튼 클릭 시*/
    [Command]
    void Unit_UpgradePre(GameObject Up1_Object)
    {
        if(Up1_Object)
        {
            Dam_Fam selectedDamFam = Up1_Object.GetComponent<Dam_Fam>();
            UnitManager_Net selectedManager = Up1_Object.GetComponent<UnitManager_Net>();
            if (selectedManager.UnitUpgradeValue < upgradeCosts_.Length)
            {
                int requiredGoldForUpgrade;
                if(selectedManager.UnitUpgradeValue < 10 )
                    requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue];
                else if(selectedManager.UnitUpgradeValue < 20 )
                    requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue] * 3;
                else
                    requiredGoldForUpgrade = upgradeCosts_[selectedManager.UnitUpgradeValue] * 5;
                int successProbability = upgradeProbabilities_[selectedManager.UnitUpgradeValue];
                if(CBossUpgrade)
                    successProbability += 5;
                float currentGold = Gold;
                if(currentGold >= requiredGoldForUpgrade)
                {
                    int randomValue = UnityEngine.Random.Range(1, 101);
                    SubtractGold(requiredGoldForUpgrade);
                    if(randomValue <= successProbability)
                    {
                        selectedManager.UnitUpgradeValue++;
                        if(selectedManager.UnitUpgradeValue == 20)
                            RpcUpgradeDone(true, Up1_Object, true);
                        else
                            RpcUpgradeDone(true, Up1_Object, false);

                        RpcUpgradeDoneLog(connectionToClient, "신비로운 힘으로 <color=#5BBB41>보호 강화</color>에 <color=#FFFF92>성공</color>하였습니다!", 0);
                    }
                    else
                    {
                        RpcUpgradeDone(false, Up1_Object, false);
                        RpcUpgradeDoneLog(connectionToClient, "<color=#5BBB41>보호 강화</color><color=#E0E0E0>에 실패하였습니다... (강화 유지)</color>", 1);
                    }
                }
                else
                {
                    RpcNotHaveGold(connectionToClient);
                }
            }
            else
            {

            }
        }
        else
        {
            RpcNotHaveUnit(connectionToClient);
        }
    }
    /*업그레이드 성공 시 클라이언트에서 진행*/
    [ClientRpc]
    private void RpcUpgradeDone(bool Done, GameObject HitT, bool UDone)
    {
        if(Done)
        {
            /*성공 이펙트 진행*/
            Vector3 hitCenter = HitT.transform.position;
            GameObject m_makedObject = Instantiate(UpgradeSuccess, hitCenter, UpgradeSuccess.transform.rotation).gameObject;
            m_makedObject.transform.SetParent(HitT.transform);

            Destroy(m_makedObject, 1f);
            /*20강 성공 컷신 진행*/
            if(UDone)
            {
                GameObject upgradeObject = Instantiate(UUpgrade_, GameObject.Find("UpgradeThing").transform);
                upgradeObject.GetComponent<Image>().sprite = HitT.GetComponent<Dam_Fam>().UUp_image;

                StartCoroutine(AnimatePosition(upgradeObject));
            }
        }
        /*실패 이펙트 진행*/
        else if(!Done)
        {
            Vector3 hitCenter = HitT.transform.position;
            GameObject m_makedObject = Instantiate(UpgradeFail, hitCenter, UpgradeFail.transform.rotation).gameObject;
            m_makedObject.transform.SetParent(HitT.transform);

            Destroy(m_makedObject, 1f);
        }
    }
    private IEnumerator AnimatePosition(GameObject upgradeObject)
    {
        RectTransform rectTransform = upgradeObject.GetComponent<RectTransform>();

        float duration = 0.4f;
        float elapsedTime = 0f;
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = new Vector2(-250, startPos.y);

        if(InGameSoundManager.Instance != null)
        {
            InGameSoundManager.Instance.UnitUpgradeSound();
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float easeInT = t * t;
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, easeInT);
            yield return null;
        }
        rectTransform.anchoredPosition = endPos;
        yield return new WaitForSeconds(2f);

        elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float easeOutT = t * t;
            rectTransform.anchoredPosition = Vector2.Lerp(endPos, startPos, easeOutT);
            yield return null;
        }

        rectTransform.anchoredPosition = startPos;
        Destroy(upgradeObject);
    }
    /*보스 생성 시 서버에서 진행*/
    public void BossDoneLog(string LogText_, bool SoundLog = false)
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        RpcBossDoneLog(opponentIdentity.connectionToClient, LogText_, SoundLog);
    }
    /*로그를 띄우고 UI애니메이션 진행*/
    [TargetRpc]
    public void RpcBossDoneLog(NetworkConnection target, string LogText_, bool SoundLog)
    {

        ShowMessage(LogText_, 3f);
        if(SoundLog == true)
        {
            BossStart_.SetActive(true);
            StartCoroutine(DeactivateGStartAfterDelay(5f, BossStart_));
            if(InGameSoundManager.Instance != null)
            {
                InGameSoundManager.Instance.BossSpawnSound();
                InGameSoundManager.Instance.BossSpawnXSound();
            }
        }
    }
    /*강화에 따른 로그 출력, 사운드 재생*/
    [TargetRpc]
    public void RpcUpgradeDoneLog(NetworkConnection target, string LogText_, int LogNum)
    {
        ShowMessage(LogText_, 3f);
        if(InGameSoundManager.Instance != null)
        {
            if(LogNum == 0)
                InGameSoundManager.Instance.UpgradeSuccessSound();
            else if(LogNum == 1)
                InGameSoundManager.Instance.UpgradeSaveSound();
            else if(LogNum == 2)
                InGameSoundManager.Instance.UpgradeDownSound();
            else if(LogNum == 3)
                InGameSoundManager.Instance.UpgradeDestroySound();
        }
            
    }
    [TargetRpc]
    public void RpcNotHaveUnit(NetworkConnection target)
    {
        ShowMessage("유닛을 찾을 수 없습니다.", 3f);
        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.NotEnoughtSound();
    }
    [TargetRpc]
    public void RpcNotHaveGold(NetworkConnection target)
    {
        ShowMessage("자원이 부족합니다.", 3f);
        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.NotEnoughtSound();
    }
    /*유닛 판매 버튼 클릭 시*/
    [Command]
    void Unit_Sell(GameObject Sell_Object)
    {
        if(Sell_Object != null)
        {
            characterPools.Remove(Sell_Object);
            Destroy(Sell_Object);
            F_Sell = true;
            CheckForAchievements();
            AddGold(50);
    }
    /*교환 버튼 클릭 시*/
    [Command]
    void CmdChange_listener(NetworkIdentity clientIdentity)
    {
        /*100골드를 소모하여 50~150 사이의 가스 획득*/
        float currentGold = Gold;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        if(currentGold >= 100)
        {
            SubtractGold(100);
            int gasToAdd = UnityEngine.Random.Range(50,151);
            AddGas(gasToAdd);
            CheckForAchievements();
            RpcChangeDone(clientIdentity.connectionToClient);
        }
        else
        {
            RpcNoGas(clientIdentity.connectionToClient);
        }
    }
    [TargetRpc]
    public void RpcChangeDone(NetworkConnection target)
    {
        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.CChangeSound();
    }
    /*종족 업그레이드 버튼 클릭 시*/
    [Command]
    public void CmdHandleUpgrade(NetworkIdentity clientIdentity, int playerID, int index)
    {
        /*종족 업그레이드 정보를 가져와서 + 1 한 값을 차감하고 해당 종족 업그레이드 진행*/
        int currentUpgradeValue = (int)upgrade_fam.upgradeValues[playerID, index];
        int upgradeCost = currentUpgradeValue + 1;

        if (Gas >= upgradeCost)
        {
            SubtractGas(upgradeCost);
            upgrade_fam.upgradeValues[playerID, index]++;
            RpcUpdateUpgradeUI(clientIdentity.connectionToClient, playerID, index, (int)upgrade_fam.upgradeValues[playerID, index]);
        }
        else
        {
            RpcNoGas(clientIdentity.connectionToClient);
        }
    }
    /*종종 강화 완료 시 UI에서 해당 내역 출력*/
    [TargetRpc]
    public void RpcUpdateUpgradeUI(NetworkConnection target, int playerID, int index, int newUpgradeValue)
    {
        if (playerID == PlayerIndex)
        {
            TMP_Text upgradeText = GetUpgradeTextComponent(index);
            if (upgradeText != null)
            {
                upgradeText.text = newUpgradeValue.ToString();
            }
            if(InGameSoundManager.Instance != null)
                InGameSoundManager.Instance.RUpgradeSound();
        }
    }
    /*종족은 두개 HL은 Light공격력, JL은 Light공격속도, HD는 Dark공격력, JD는 Dark공격속도*/
    private TMP_Text GetUpgradeTextComponent(int index)
    {
        switch (index)
        {
            case 0:
                return gamethings._HL_Num;
            case 1:
                return gamethings._JL_Num;
            case 2:
                return gamethings._HD_Num;
            case 3:
                return gamethings._JD_Num;
            default:
                Debug.LogError("Invalid index for upgrade text!");
                return null;
        }
    }

    private Button GetUpgradeButtonComponent(int index)
    {
        switch (index)
        {
            case 0:
                return gamethings.HL_Btn;
            case 1:
                return gamethings.JL_Btn;
            case 2:
                return gamethings.HD_Btn;
            case 3:
                return gamethings.JD_Btn;
            default:
                Debug.LogError("Invalid index for upgrade button!");
                return null;
        }
    }
    /*종족업그레이드 값(4) * 유저 인원(4) 리스트*/
    [Command]
    private void CmdDamFam(NetworkIdentity clientIdentity)
    {
        List<float> serializedUpgradeValues = new List<float>();

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                serializedUpgradeValues.Add(upgrade_fam.upgradeValues[i, j]);
            }
        }
        RpcDamFam(clientIdentity.connectionToClient, serializedUpgradeValues.ToArray());
    }
    /*해당 정보는 클라이언트에도 저장 ToArray()시 0,1 index에*/
    [TargetRpc]
    private void RpcDamFam(NetworkConnection target, float[] serializedUpgradeValues)
    {
        int index = 0;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                selectchange.upgradeValues[i, j] = serializedUpgradeValues[index++];
            }
        }
    }
    /*Enemy가 죽을 때 확률적으로 진행*/
    public void AddLoot(string lootItem)
    {
        int index = -1;
        for (int i = 0; i < Lootinventory.Count; i++)
        {
            if (Lootinventory[i].ItemName == lootItem)
            {
                index = i;
                break;
            }
        }
        if(index != -1)
        {
            LootItem MyLootItem = Lootinventory[index];
            Lootinventory[index] = new LootItem
            {
                ItemName = MyLootItem.ItemName,
                ItemCount = MyLootItem.ItemCount + 1
            };
        }
        else
        {
            Lootinventory.Add(new LootItem
            {
                ItemName = lootItem,
                ItemCount = 1
            });
        }
    }
    /*게임 패배 시 서버측에서 진행*/
    public void Def_Game()
    {
        bool allDead = true;
        Scene targetScene = gameObject.scene;
        /*모든 유저가 패배했는지 확인*/
        foreach (GameObject obj in targetScene.GetRootGameObjects())
        {
            GameData gameData = obj.GetComponentInChildren<GameData>(true);
            if (gameData != null && gameData != this && !gameData.F_Death)
            {
                allDead = false;
                break;
            }
        }
        /*모든 유저가 패배했을 경우 관전이 불가능하게 하기 위함*/
        if (allDead)
        {
            Def_Client(connectionToClient, RoundNum+1, Total_BC, Total_UC, true);
        }
        else
        {
            Def_Client(connectionToClient, RoundNum+1, Total_BC, Total_UC, false);
        }
        /*패배한 유저 재화를 초기화, 적 삭제*/
        Gold = 0;
        Gas = 0;
        foreach (var enemy in FindObjectsOfType<enemy>())
        {
            if (enemy != null && enemy.playerIndex == this.PlayerIndex)
            {
                Destroy(enemy.gameObject);
            }
        }
    }
    /*승리 시 서버측에서 진행*/
    public void Vic_Game()
    {
        Vic_Client(connectionToClient, 40, Total_BC, Total_UC, false);
        Gold = 0;
        Gas = 0;
        foreach (var enemy in FindObjectsOfType<enemy>())
        {
            if (enemy != null && enemy.playerIndex == this.PlayerIndex)
            {
                Destroy(enemy.gameObject);
            }
        }
    }
    [TargetRpc]
    public void Vic_Client(NetworkConnection target, int Rnum, int BC, int UC, bool AllDead)
    {
        /*승리, 패배 애니메이션을 진행할 판넬 활성화*/
        gamethings.EndPanel.SetActive(true);
        /*보유한 유닛을 보여주기 위해 리스트 생성 및 이미지 추가*/
        List<Sprite> LastC_Names = new List<Sprite>();
        foreach (var character in Globals_Net.CurrentExist_UNITS)
        {
            if (character != null)
            {
                Dam_Fam damFamComponent = character.GetComponent<Dam_Fam>();
                if (damFamComponent != null)
                {
                    LastC_Names.Add(damFamComponent.Unit_image);
                }
            }
        }
        /*유닛 선택및 보유 초기화, 게임 진행시간 기록*/
        Globals_Net.CurrentExist_UNITS.Clear();
        Globals_Net.SELECTED_UNITS.Clear();
        string Time_string = ingameTimer.GetTimerText();
        Image gameEndImage = GameEnd_Panel.GetComponent<Image>();
        if (gameEndImage != null)
        {
            /*패배, 승리에 따른 애니메이션 진행 및 결과창 띄움*/
            StartCoroutine(FadeInImage(gameEndImage, 1f, true, Time_string, Rnum, BC, UC, LastC_Names, AllDead));
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        Def_Recive(opponentIdentity);
    }
    [TargetRpc]
    public void Def_Client(NetworkConnection target, int Rnum, int BC, int UC, bool AllDead)
    {
        /*Vic_Client와 통합 필요*/
        gamethings.EndPanel.SetActive(true);
        List<Sprite> LastC_Names = new List<Sprite>();
        foreach (var character in Globals_Net.CurrentExist_UNITS)
        {
            if (character != null)
            {
                Dam_Fam damFamComponent = character.GetComponent<Dam_Fam>();
                if (damFamComponent != null)
                {
                    LastC_Names.Add(damFamComponent.Unit_image);
                }
            }
        }
        Globals_Net.CurrentExist_UNITS.Clear();
        Globals_Net.SELECTED_UNITS.Clear();
        string Time_string = ingameTimer.GetTimerText();
        Image gameEndImage = GameEnd_Panel.GetComponent<Image>();
        if (gameEndImage != null)
        {
            StartCoroutine(FadeInImage(gameEndImage, 1f, false, Time_string, Rnum, BC, UC, LastC_Names, AllDead));
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        Def_Recive(opponentIdentity);
    }
    /*캐릭터 제거*/
    [Command]
    public void Def_Recive(NetworkIdentity clientIdentity)
    {
        foreach (var character in characterPools)
        {
            if (character != null)
            {
                Destroy(character);
            }
        }
    }
    IEnumerator FadeInImage(Image image, float duration, bool Vic, string Tstring, int Rnum, int BC, int UC, List<Sprite> LastC, bool AllDead)
    {
        float currentTime = 0f;
        Color color = image.color;

        while (currentTime < duration)
        {
            float alpha = Mathf.Lerp(0, 1, currentTime / duration);
            image.color = new Color(color.r, color.g, color.b, alpha);
            currentTime += Time.deltaTime;
            yield return null;
        }
        image.color = new Color(color.r, color.g, color.b, 1);
        if(!Vic)
        {
            /*패배 시 패배 애니메이션 진행, Result_Panel은 초기 scale이 0*/
            gamethings.Def_.SetActive(true);
            gamethings.Result_Panel.SetActive(true);
            GameObject Result_D = Result_Panel.transform.Find("Panel/Result_D").gameObject;
            Result_D.SetActive(true);
            /*모두 패배한 경우 관전 비활성화*/
            if(AllDead)
            {
                GameObject Wacth_Button = Result_Panel.transform.Find("Panel/Wacth_Button").gameObject;
                Wacth_Button.SetActive(false);
            }
            /*진행 중인 유저가 있는 경우 관전 활성화*/
            else
            {
                Button WatchButton = Result_Panel.transform.Find("Panel/Wacth_Button").GetComponent<Button>();
                WatchButton.onClick.AddListener(WatchButtonClicked);
            }
        }
        else
        {
            /*승리 시 승리 애니메이션 진행, Result_Panel은 초기 scale이 0*/
            gamethings.Vic_.SetActive(true);
            gamethings.Result_Panel.SetActive(true);
            GameObject Result_V = Result_Panel.transform.Find("Panel/Result_V").gameObject;
            Result_V.SetActive(true);
            GameObject Wacth_Button = Result_Panel.transform.Find("Panel/Wacth_Button").gameObject;
            Wacth_Button.SetActive(false);
        }
        /*결과창 이미지, 레벨, 경험치 등 정보를 입력, 슬라이더를 두개 사용하여 기존 경험치에 추가되는 경험치가 올라가는 효과 적용*/
        Image Result_Img = Result_Panel.transform.Find("Panel/User_Image").GetComponent<Image>();
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(Gameinventory[this.PlayerIndex].PlayerTexture);
        Result_Img.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            
        TMP_Text Result_LV = Result_Panel.transform.Find("Panel/LV_Text").GetComponent<TMP_Text>();
        int AddExp = Rnum * UnityEngine.Random.Range(8, 16);
        TMP_Text Result_Exp = Result_Panel.transform.Find("Panel/EXP_Add").GetComponent<TMP_Text>();
        Result_Exp.text = $"+ {AddExp}";
        Image CuSlider = Result_Panel.transform.Find("Panel/Exp_Image/Slider_Current").GetComponent<Image>();
        Image AdSlider = Result_Panel.transform.Find("Panel/Exp_Image/Slider_Add").GetComponent<Image>();

        int level = ClientDataManager.Instance.UserDetails.Level;
        float MaxExp = level * 100f;
        float CurrentExp = ClientDataManager.Instance.UserDetails.ExperiencePoints;
        float NewTotalExp = CurrentExp + AddExp;
        /*레벨 당 경험치는 레벨*100, 경험치를 얻어, 레벨이 증가할 경우와 연속 레벨업을 위함*/
        if (NewTotalExp >= MaxExp)
        {
            while (NewTotalExp >= MaxExp)
            {
                NewTotalExp -= MaxExp;
                level++;
                MaxExp = level * 100f;
            }
            Result_LV.text = $"Lv. {level}";
            /*레벨 업 할 경우 기존 경험치는 0으로 두고 추가 경험치만 표시*/
            CuSlider.fillAmount = 0;
            StartCoroutine(UpdateFillAmount(AdSlider, 0, NewTotalExp / MaxExp, 1.2f));
            GameObject LvUp_Image = Result_Panel.transform.Find("Panel/LvUp_Image").gameObject;
            LvUp_Image.SetActive(true);
        }
        else
        {
            Result_LV.text = $"Lv. {level}";
            /*레벨 업을 안할 경우*/
            StartCoroutine(UpdateFillAmount(CuSlider, 0, CurrentExp / MaxExp, 1f));
            StartCoroutine(UpdateFillAmount(AdSlider, 0, NewTotalExp / MaxExp, 1.2f));
        }

        TMP_Text Result_Nick = Result_Panel.transform.Find("Panel/NICK_Text").GetComponent<TMP_Text>();
        Result_Nick.text = ClientDataManager.Instance.UserDetails.Nickname;

        GameObject Constant = Result_Panel.transform.Find("Panel/Constant_thing").gameObject;
        TMP_Text P_Time = Constant.transform.Find("P_Time").GetComponent<TMP_Text>();
        P_Time.text = Tstring;
        TMP_Text P_Round = Constant.transform.Find("P_Round").GetComponent<TMP_Text>();
        P_Round.text = $"{Rnum} Round";
        TMP_Text P_UC = Constant.transform.Find("P_UC").GetComponent<TMP_Text>();
        P_UC.text = UC.ToString();
        TMP_Text P_BC = Constant.transform.Find("P_BC").GetComponent<TMP_Text>();
        P_BC.text = BC.ToString();
        TMP_Text P_Ach = Constant.transform.Find("P_Ach").GetComponent<TMP_Text>();
        int achievedCount = 0;
        foreach (var achievement in Achievementinventory)
        {
            if (achievement.achieved)
            {
                achievedCount++;
            }
        }
        P_Ach.text = $"{achievedCount} 회";
        int BP_ADD = achievedCount*10 + Rnum*10;
        TMP_Text Bp_text = Constant.transform.Find("Bp_Add").GetComponent<TMP_Text>();
        Bp_text.text = $"+ {BP_ADD}";

        TMP_Text P_HL = Constant.transform.Find("P_HL").GetComponent<TMP_Text>();
        P_HL.text = $"Lv. {gamethings._HL_Num.text}";
        TMP_Text P_HD = Constant.transform.Find("P_HD").GetComponent<TMP_Text>();
        P_HD.text = $"Lv. {gamethings._HD_Num.text}";
        TMP_Text P_JL = Constant.transform.Find("P_JL").GetComponent<TMP_Text>();
        P_JL.text = $"Lv. {gamethings._JL_Num.text}";
        TMP_Text P_JD = Constant.transform.Find("P_JD").GetComponent<TMP_Text>();
        P_JD.text = $"Lv. {gamethings._JD_Num.text}";

        GameObject Cha_infos = Result_Panel.transform.Find("Panel/Cha_Info").gameObject;
        GameObject Cha_over = Result_Panel.transform.Find("Panel/Cha_Over").gameObject;
        Dictionary<string, int> spriteCounts = new Dictionary<string, int>();
        foreach (var sprite in LastC)
        {
            if (spriteCounts.ContainsKey(sprite.name))
            {
                spriteCounts[sprite.name]++;
            }
            else
            {
                spriteCounts.Add(sprite.name, 1);
            }
        }
        /*같은 유닛이 있을 경우 이미지 * 숫자로 표시하기 위함*/
        var sortedSpriteCounts = spriteCounts.OrderByDescending(kv =>
        {
            int numberPart = int.Parse(kv.Key.Substring(1));
            return numberPart;
        })
        .ThenBy(kv =>
        {
            return kv.Key[0];
        })
        .ToList();
        int maxResultCha = 18;
        int count = 0;
        foreach (var spriteCount in sortedSpriteCounts)
        {
            if (count >= maxResultCha)
            {
                Cha_over.SetActive(true);
                break;
            }

            Sprite sprite = LastC.Find(s => s.name == spriteCount.Key);
            GameObject newResultCha = Instantiate(Result_Cha, Cha_infos.transform);
            Image resultImage = newResultCha.GetComponent<Image>();
            if (resultImage != null)
            {
                resultImage.sprite = sprite;
            }
            TMP_Text countText = newResultCha.transform.Find("Text").GetComponent<TMP_Text>();
            if (countText != null)
            {
                countText.text = $"x {spriteCount.Value}";
            }

            count++;
        }
        if (count < maxResultCha)
        {
            Cha_over.SetActive(false);
        }

        GameObject Item_infos = Result_Panel.transform.Find("Panel/Item_Info").gameObject;
        foreach(LootItem item in Lootinventory)
        {
            GameObject newResultItem = Instantiate(Result_Item, Item_infos.transform);
            Image resultItemImage = newResultItem.GetComponent<Image>();
            if (resultItemImage != null)
            {
                resultItemImage.sprite = Resources.Load<Sprite>($"LootImages/{item.ItemName}");
            }
            TMP_Text ItemcountText = newResultItem.transform.Find("Text").GetComponent<TMP_Text>();
            if (ItemcountText != null)
            {
                ItemcountText.text = $"{item.ItemCount}";
            }
            /*
            GameData_Server.UpdateLootItem updateItem = new GameData_Server.UpdateLootItem
            {
                ItemName = item.ItemName,
                ItemCount = item.ItemCount
            };
            updateLootItems.Add(updateItem);
            */
        }

        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        string Uid = ClientDataManager.Instance.UserDetails.userID;
        //CmdSendItemData(updateLootItems, opponentIdentity, Uid);
        /*경험치와 레벨, 획득재화를 DB에 반영*/
        CmdSendGameStats(opponentIdentity, Uid, BP_ADD, (int)NewTotalExp, level);

        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.EndGameSound();
        
        yield return new WaitForSeconds(0.75f);

        if(InGameSoundManager.Instance != null)
        {
            if(!Vic)
                InGameSoundManager.Instance.GVoiceSound(1);
            else
                InGameSoundManager.Instance.GVoiceSound(2);
        }
            
        /*승리, 패배 애니메이션이 진행되는 3초 기다림*/
        yield return new WaitForSeconds(3f);
        //yield return new WaitForSeconds(3.75f);

        /*내가 패배함으로 유저 전부가 패배한 경우*/
        if(AllDead)
        {
            CmdAllDeadCheck(opponentIdentity);
        }
        /*승리한 경우*/
        if(!AllDead && Vic)
        {
            CmdAllDeadCheck_Vic(opponentIdentity);
        }

        StartCoroutine(WaitForResponsesBeforeScalingPanel(Result_Panel, AllDead, Vic));
    }
    private IEnumerator UpdateFillAmount(Image image, float startValue, float endValue, float duration)
    {
        yield return new WaitForSeconds(4f);
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            image.fillAmount = Mathf.Lerp(startValue, endValue, elapsedTime / duration);
            yield return null;
        }
        image.fillAmount = endValue;
    }
    /*게임 정보가 정상적으로 DB에 완료된 경우 결과창 스케일 증가*/
    IEnumerator WaitForResponsesBeforeScalingPanel(GameObject Result_Panel, bool AllDead, bool Vic)
    {
        float timeout = 30f;
        while ((!GameResponeDone) && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        L_Coroutine = StartCoroutine(PanelScaleObject(Result_Panel, Vector3.zero, Vector3.one, 0.2f, AllDead, Vic));
    }
    /*관전할 것이 없으므로 승리한 유저가 아닌 유저를 10초 후 유저를 로비로 보냄*/
    [Command]
    public void CmdAllDeadCheck_Vic(NetworkIdentity clientIdentity)
    {
        Scene targetScene = gameObject.scene;
        foreach (GameObject obj in targetScene.GetRootGameObjects())
        {
            GameData gameData = obj.GetComponentInChildren<GameData>(true);
            if (gameData != null && gameData != this && gameData.F_Death)
            {
                NetworkIdentity targetIdentity = gameData.gameObject.GetComponent<NetworkIdentity>();
                if(targetIdentity != null && targetIdentity.connectionToClient != null)
                {
                    gameData.RpcAllDead(targetIdentity.connectionToClient);
                }
            }
        }
    }
    /*나를 제외한 모든 유저가 패배한 후 관전 중이므로 관전 중인 유저들을 10초 후 로비로 보냄*/
    [Command]
    public void CmdAllDeadCheck(NetworkIdentity clientIdentity)
    {
        Scene targetScene = gameObject.scene;
        foreach (GameObject obj in targetScene.GetRootGameObjects())
        {
            GameData gameData = obj.GetComponentInChildren<GameData>(true);
            if (gameData != null && gameData != this)
            {
                NetworkIdentity targetIdentity = gameData.gameObject.GetComponent<NetworkIdentity>();
                if(targetIdentity != null && targetIdentity.connectionToClient != null)
                {
                    gameData.RpcAllDead(targetIdentity.connectionToClient);
                }
            }
        }
    }
    [TargetRpc]
    public void RpcAllDead(NetworkConnection target)
    {
        StartCoroutine(AllDeadCor());
    }
    IEnumerator AllDeadCor()
    {
        int countdownTime = 10;
        while (countdownTime > 0)
        {
            gamethings.ToLobby_Text.text = $"게임종료 : {countdownTime} 초 후 방으로 이동 합니다.";
            yield return new WaitForSeconds(1);
            countdownTime--;
        }
        gamethings.ToLobby_Text.text = $"게임종료 : 방으로 이동 합니다.";
        var clientGameManger = FindObjectOfType<Insight.ClientGameManager_Kihana_1>();
        if (clientGameManger != null)
        {
            CmdDischange();
            clientGameManger.SendJoinGameMsg("LeaveToRoom");
        }
    }
    IEnumerator PanelScaleObject(GameObject obj, Vector3 fromScale, Vector3 toScale, float duration, bool AllDead, bool Vic)
    {
        float counter = 0f;
        obj.transform.localScale = fromScale;

        while (counter < duration)
        {
            counter += Time.deltaTime;
            obj.transform.localScale = Vector3.Lerp(fromScale, toScale, counter / duration);
            yield return null;
        }

        obj.transform.localScale = toScale;
        if(InGameSoundManager.Instance != null)
            InGameSoundManager.Instance.ResultGameSound();


        TMP_Text Exit_Text = Result_Panel.transform.Find("Panel/Exit_Text").GetComponent<TMP_Text>();
        int countdownTime = 10;
        /*모두 패배한 경우, 승리한 경우 다 같이 방으로 이동 가능*/
        if(AllDead || (!AllDead && Vic))
        {
            while (countdownTime > 0)
            {
                Exit_Text.text = $"{countdownTime}초 후 방으로 이동 합니다.";
                yield return new WaitForSeconds(1);
                countdownTime--;
                
            }
            var clientGameManger = FindObjectOfType<Insight.ClientGameManager_Kihana_1>();
            if (clientGameManger != null)
            {
                CmdDischange();
                clientGameManger.SendJoinGameMsg("LeaveToRoom");
            }
            Exit_Text.text = $"방으로 이동 합니다."; 
        }
        else
        {
            while (countdownTime > 0)
            {
                Exit_Text.text = $"{countdownTime}초 후 로비로 이동 합니다.";
                yield return new WaitForSeconds(1);
                countdownTime--;
            }
            var clientGameManger = FindObjectOfType<Insight.ClientGameManager_Kihana_1>();
            if (clientGameManger != null)
            {
                CmdDischange();
                clientGameManger.SendJoinGameMsg("LeaveToLobby");
            }
            Exit_Text.text = $"로비로 이동 합니다."; 
        }
    }
    void WatchButtonClicked()
    {
        gamethings.EndPanel.SetActive(false);
        gamethings.UnderBoard.SetActive(false);
        gamethings.ToLobbyPanel.SetActive(true);
        Button ToLobby_Button = ToLobbyPanel.transform.Find("Button_ToLobby").GetComponent<Button>();
        ToLobby_Button.onClick.AddListener(ToLobby_ButtononClicked);
        if (L_Coroutine != null)
        {
            StopCoroutine(L_Coroutine);
        }
    }
    void ToLobby_ButtononClicked()
    {
        var clientGameManger = FindObjectOfType<Insight.ClientGameManager_Kihana_1>();
        if (clientGameManger != null)
        {
            CmdDischange();
            clientGameManger.SendJoinGameMsg("LeaveToLobby");
        }
    }
    [Command]
    void CmdDischange()
    {
        this.LeaveState = false;
    }
}
