using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Tests
{
    public class DataAssetTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [Test]
        public void WeaponData_IsAssignableToItemDataField()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            ItemData asBase = weapon;

            Assert.IsNotNull(asBase);
            Assert.AreEqual(ItemType.Weapon, asBase.itemType);

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void WeaponData_Defaults()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();

            Assert.AreEqual(1, weapon.maxStack);
            Assert.AreEqual(Vector3.one, weapon.socketLocalScale);
            Assert.AreEqual(0, weapon.damage);

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void QuestData_DefaultsToThreeRequired()
        {
            QuestData quest = ScriptableObject.CreateInstance<QuestData>();

            Assert.AreEqual(3, quest.requiredAmount);
            Assert.IsTrue(string.IsNullOrEmpty(quest.targetEnemyTypeId));

            Object.DestroyImmediate(quest);
        }

        [Test]
        public void DialogueData_EmptyOptionsByDefault()
        {
            DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();

            Assert.IsNull(dialogue.options);
            Assert.IsNull(dialogue.lines);

            Object.DestroyImmediate(dialogue);
        }

        [Test]
        public void EnemyDiedEvent_CarriesDropPayload()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            var evt = new EnemyDiedEvent("LizardWarrior", new Vector3(1f, 2f, 3f), weapon);

            Assert.AreEqual("LizardWarrior", evt.EnemyTypeId);
            Assert.AreEqual(3f, evt.Position.z);
            Assert.AreSame(weapon, evt.Drop);

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void InteractionPromptChangedEvent_HoldsTextAndVisibility()
        {
            var evt = new InteractionPromptChangedEvent("对话", true);

            Assert.AreEqual("对话", evt.PromptText);
            Assert.IsTrue(evt.Visible);
        }
    }
}
