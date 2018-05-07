﻿using GameplayNs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameUiNs
{
    public class UIController : EventsSubscriber
    {
        public GameObject DeathPanel;
        public GameObject GameOverPanel;
        public ShotButton ShotBtn;
        public GameObject CrossHairPanel;
        public GameInfoPanel InfoPanel;
       
        Button shotBtn;

        void Start()
        {
            shotBtn = ShotBtn.GetComponent<Button>();
        }
        protected override void Update()
        {
            base.Update();
        }

        override protected void NotifySomeDataChanged()
        {

        }

        override protected void NotifySomethingHappened(GameData.SomethingId id)
        {
            switch (id)
            {
                case GameData.SomethingId.PlayerDied:
                    DeathPanel.SetActive(true);
                    ShotBtn.StopFiring();

                    ShotBtn.enabled = false;
                    shotBtn.interactable = false;


                    break;

                case GameData.SomethingId.GameOver:
                    DeathPanel.SetActive(false);
                    GameOverPanel.SetActive(true);
                    ShotBtn.StopFiring();
                    ShotBtn.enabled = false;
                    shotBtn.interactable = false;
                    break;

                case GameData.SomethingId.PlayerResurrected:
                    DeathPanel.SetActive(false);
                    ShotBtn.enabled = true;
                    shotBtn.interactable = true;
                    break;

                case GameData.SomethingId.GameStart:
                    ShotBtn.gameObject.SetActive(true);
                    ShotBtn.enabled = true;
                    shotBtn.interactable = true;
                    InfoPanel.gameObject.SetActive(true);
                    CrossHairPanel.gameObject.SetActive(false);
                    break;

            }
        }



    }
}