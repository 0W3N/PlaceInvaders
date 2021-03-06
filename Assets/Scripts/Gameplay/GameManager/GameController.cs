﻿using EnemiesNs;
using PlayerNs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeaponNs;
using PunServerNs;
using Placenote;

namespace GameplayNs
{
    /// <summary>
    /// Mostly just mediator to simplify development.
    /// "Scene-only Singleton"  -  cleared and destroyed when game object deleted.
    /// Initialized at Awake so should not be called from other Awake() functions except
    /// generated at scene definitelly after initialization.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public int InitialPlayerLifes = 10;
        public ServerController Server;
        public GameSetupController GameSetup;

        [Header ("Setup enemies here")]
        public EnemyLibrary Lib;

        [Header ("Public for debug purposes only")]
        public List<RespawnPoint> Respawns;
        public GameData _data;
        [Range (5, 20)]
        public float spawnDistance;

        // Use this for initialization
        #region Standard Unity functions except used for Singletone implementation

        bool RunOnce = true;

        void Start ()
        {
            if (Server == null)
                Server = FindObjectOfType<ServerController> ();
            if (GameSetup == null)
                GameSetup = FindObjectOfType<GameSetupController> ();
        }

        /// <summary>
        /// To do some initialization which requires that all Awake(), Start() etc
        /// was already good place to wait network connection before starting gameplay
        /// </summary>
        IEnumerator Initialize ()
        {
            yield return null;
            Data.Lives = InitialPlayerLifes;
            Data.Kills = 0;
        }

        private void Update ()
        {
            if (RunOnce)
            {
                RunOnce = false;
                StartCoroutine (Initialize ());
            }
        }

        #endregion Standard Unity functions except used for Singletone implementation


        #region World root

        /// <summary>
        /// direct changing position/rotation is forbidden. Reason:
        /// it have to be done either by positioning system or with initial setup of location
        /// Use SetupWorldRootCoordinates or _DevicePos, whatever will work
        /// </summary>
        public static WorldRoot WorldRootObject
        {
            get; private set;
        }

        /// <summary>
        /// called on initial setup procedures, during gameplay world
        /// coordinates can be updated without coordinates and non-stop
        /// </summary>
        /// <param name="newPos"> new initial world root position </param>
        /// <param name="newRot"> new initial world root rotation </param>
        public static void SetupWorldRootCoordinates (Vector3 newPos, Quaternion newRot)
        {
            WorldRootObject.SetupWorldRootCoordinates (newPos, newRot);
        }

        #endregion World root


        #region Other static members

        public static GameData Data
        {
            get { return (Instance == null ? null : Instance._data); }
        }

        public static bool IsGamePlaying
        {
            get { return GameController.Data.GameState == GameStateId.GamePlaying; }
        }

        public static string EnemyTag
        {
            get { return Instance.Lib.EnemyTag; }
        }

        public static string EnemyDamageReceiverName
        {
            get { return Instance.Lib.DamageReceiverMethodName; }
        }

        public PlayerController _currentPlayer;
        public static PlayerController CurrentPlayer
        {
            get { return Instance._currentPlayer; }
            private set { Instance._currentPlayer = value; }
        }

        public static void RegisterRespawnPoint (RespawnPoint newRespawn)
        {
            if (!Instance.Respawns.Contains (newRespawn))
                Instance.Respawns.Add (newRespawn);
        }

        public List<PlayerController> Players;

        public static void RegisterPlayer (PlayerController player)
        {
            Debug.Log ("Registering player");
            if (player.IsLocalPlayer || player.IsCurrentNetworkPlayer)
            {
                CurrentPlayer = player;
            }
            int idx = Instance.Players.FindIndex ((el) => el != null && el.Equals (player));
            if (idx >= 0)
                Instance.Players[idx] = player;
            else
                Instance.Players.Add (player);

            Debug.Log ("------------ Registered  player " + player.gameObject.name + " --------------");

        }

        public static void RemovePlayer (PlayerController player)
        {
            if (player == null)
                Instance.Players.RemoveAll (el => el == null);
            if (player.Equals (CurrentPlayer))
                CurrentPlayer = null;
            if (Instance.Players.Remove (player))
                Debug.Log ("------------ Removed  player " + player.gameObject.name + " --------------");
            else
                Debug.Log ("------------ Failed Removing  player " + player.gameObject.name + "--------------");
        }

        public static PlayerController GetRandomPlayer ()
        {
            var filteredPlayers = Instance.Players.FindAll (el => el != null && !el.IsDead);
            Debug.Log ("FilteredPlayer Count Is: " + filteredPlayers.Count);
            if (filteredPlayers.Count == 0)
                return null;
            int idx = Random.Range (0, filteredPlayers.Count);
            return filteredPlayers[idx];
        }

        public static void PrepareGame ()
        {
            Data.Reset ();
            Data.Lives = Instance.InitialPlayerLifes;
            Data.Kills = 0;
            Data.OnPrepareGame ();
        }

        public static void StartGame ()
        {
            SetupWorldRootCoordinates (Instance._currentPlayer.transform.position, Instance._currentPlayer.transform.rotation);
            Data.OnStartGame ();
        }

        #endregion  Static members

        public void QuitGame ()
        {
            if (PhotonNetwork.connected)
            {
                if (GameSetup.IsLocalized)
                    Server.TotalLocalizedPlayers = Server.TotalLocalizedPlayers - 1;
            }
            // Remove enemies
            RemoveAllEnemies ();
            // Remove player is handeled by PlayerPhotonGenerator
            EnvironmentScannerController.Instance.StopUsingMap ();
            Server.QuitToMainMenu ();
            Data.OnToMainMenu ();
        }


        #region Weapons

        /// <summary>
        /// Current weapon
        /// </summary>
        public WeaponController Weapon
        {
            get { return CurrentPlayer.CurrentWeapon; }
        }

        #endregion Weapons


        #region Enemy Spawning and Despawning

        public static void CreateRandomEnemy ()
        {
            Instance.__CreateRandomEnemyAtPoint ();
        }

        public static void RemoveAllEnemies ()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag ("Enemy");

            for (int i = 0; i < enemies.Length; i++)
            {
                Destroy (enemies[i]);
            }
        }

        #endregion

        #region wrappers for using with Unity Event triggers

        public void __CreateRandomEnemyAtPoint ()
        {
            if (CurrentPlayer.IsDead && CurrentPlayer.IsLocalPlayer)
                return;
            if (!CurrentPlayer.IsLocalPlayer && !PhotonNetwork.isMasterClient)
                return;

            var instance = Instance;

            var prefab = Instance.Lib.GetRandomEnemy ().prefab;

            GameObject newEnemy = null;
            int angle = Random.Range (0, 359);
            if (CurrentPlayer.IsLocalPlayer)
            {
                newEnemy = Instantiate (prefab, WorldRootObject.transform, false);
                newEnemy.transform.position += new Vector3 (spawnDistance * Mathf.Sin (angle), 0, spawnDistance * Mathf.Cos (angle));
                newEnemy.transform.LookAt (Vector3.zero);
            }
            else
            {
                newEnemy = PhotonNetwork.InstantiateSceneObject (
                        prefab.name,
                        new Vector3 (spawnDistance * Mathf.Sin (angle), 0, spawnDistance * Mathf.Cos (angle)),
                        Quaternion.identity,
                        0, null);
                newEnemy.transform.LookAt (Vector3.zero);
                newEnemy.transform.parent = WorldRootObject.transform;
            }
            Data.TotalEnemies++;
        }

        #endregion  wrappers for using with Unity Event triggers


        #region Singleton features implementation

        private static GameController instance;
        public static GameController Instance
        {
            get { return instance; }
        }

        /// <summary>
        /// Returns whether the instance has been initialized or not.
        /// </summary>
        public static bool IsInitialized
        {
            get { return instance != null; }
        }

        /// <summary>
        /// Base awake method that sets the singleton's unique instance.
        /// </summary>
        protected virtual void Awake ()
        {
            if (instance != null)
            {
                Debug.LogError ("Trying to instantiate a second instance of  GameController singleton ");
            }
            else
            {
                _data = _data ?? new GameData ();
                instance = this;
            }
            WorldRootObject = FindObjectOfType<WorldRoot> ();
        }

        protected virtual void OnDestroy ()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion Singletone Implementation
    }
}