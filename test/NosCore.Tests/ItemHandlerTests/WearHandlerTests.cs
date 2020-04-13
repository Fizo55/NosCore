﻿//  __  _  __    __   ___ __  ___ ___
// |  \| |/__\ /' _/ / _//__\| _ \ __|
// | | ' | \/ |`._`.| \_| \/ | v / _|
// |_|\__|\__/ |___/ \__/\__/|_|_\___|
// 
// Copyright (C) 2019 - NosCore
// 
// NosCore is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NosCore.Packets.ClientPackets.Inventory;
using NosCore.Packets.Enumerations;
using NosCore.Packets.ServerPackets.Chats;
using NosCore.Packets.ServerPackets.Inventory;
using NosCore.Packets.ServerPackets.Player;
using NosCore.Packets.ServerPackets.Specialists;
using NosCore.Packets.ServerPackets.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NosCore.Core;
using NosCore.Core.I18N;
using NosCore.Data;
using NosCore.Data.Enumerations;
using NosCore.Data.Enumerations.I18N;
using NosCore.Data.Enumerations.Items;
using NosCore.Data.StaticEntities;
using NosCore.GameObject;
using NosCore.GameObject.Providers.InventoryService;
using NosCore.GameObject.Providers.ItemProvider;
using NosCore.GameObject.Providers.ItemProvider.Handlers;
using NosCore.GameObject.Providers.ItemProvider.Item;
using NosCore.Tests.Helpers;
using Serilog;

namespace NosCore.Tests.ItemHandlerTests
{
    [TestClass]
    public class WearEventHandlerTests : UseItemEventHandlerTestsBase
    {
        private ItemProvider? _itemProvider;
        private Mock<ILogger>? _logger;

        [TestInitialize]
        public async Task SetupAsync()
        {
            _logger = new Mock<ILogger>();
            TestHelpers.Instance.WorldConfiguration.BackpackSize = 40;
            Session = await TestHelpers.Instance.GenerateSessionAsync().ConfigureAwait(false);
            Handler = new WearEventHandler(_logger.Object);
            var items = new List<ItemDto>
            {
                new Item
                {
                    VNum = 1,
                    Type = NoscorePocketType.Equipment, 
                    ItemType = ItemType.Weapon,
                    RequireBinding = true
                },
                new Item
                { 
                    VNum = 2,
                    Type = NoscorePocketType.Equipment, 
                    EquipmentSlot = EquipmentType.Fairy,
                    Element = ElementType.Water
                },
                new Item
                {  
                    VNum = 3,
                    Type = NoscorePocketType.Equipment,
                    EquipmentSlot = EquipmentType.Fairy,
                    Element = ElementType.Fire
                },
                new Item
                { 
                    VNum = 4,
                    Type = NoscorePocketType.Equipment,
                    ItemType = ItemType.Specialist,
                    EquipmentSlot = EquipmentType.Sp,
                    Element = ElementType.Fire
                },
                new Item
                {
                    VNum = 5,
                    Type = NoscorePocketType.Equipment,
                    ItemType = ItemType.Weapon,
                    RequireBinding = true,
                    Sex = 2
                }, 
                new Item
                {
                    VNum = 6,
                    Type = NoscorePocketType.Equipment,
                    ItemType = ItemType.Specialist,
                    EquipmentSlot = EquipmentType.Sp,
                    LevelJobMinimum = 2,
                    Element = ElementType.Fire
                },   
                new Item
                {
                    VNum = 7,
                    Type = NoscorePocketType.Equipment,
                    ItemType = ItemType.Jewelery,
                    ItemValidTime = 50,
                    EquipmentSlot = EquipmentType.Amulet
                }
            };
            _itemProvider = new ItemProvider(items,
                new List<IEventHandler<Item, Tuple<InventoryItemInstance, UseItemPacket>>>());
        }

        [TestMethod]
        public async Task Test_Can_Not_Use_WearEvent_In_ShopAsync()
        {
            Session!.Character.InShop = true;
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(1), Session.Character.CharacterId);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            _logger!.Verify(s => s.Error(LogLanguage.Instance.GetMessageFromKey(LogLanguageKey.CANT_USE_ITEM_IN_SHOP)), Times.Exactly(1));
        }

        [TestMethod]
        public async Task Test_BoundCharacter_QuestionAsync()
        {
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(1), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (QnaPacket?)Session.LastPackets.FirstOrDefault(s => s is QnaPacket);
            Assert.AreEqual(Session.GetMessageFromKey(LanguageKey.ASK_BIND), lastpacket?.Question);
        }

        [TestMethod]
        public async Task Test_BoundCharacterAsync()
        {
            UseItem.Mode = 1;
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(1), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            Assert.AreEqual(Session.Character.CharacterId, itemInstance.ItemInstance?.BoundCharacterId);
        }

        [TestMethod]
        public async Task Test_BadEquipmentAsync()
        {
            UseItem.Mode = 1;
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(5), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (SayPacket?)Session.LastPackets.FirstOrDefault(s => s is SayPacket);
            Assert.AreEqual(Session.GetMessageFromKey(LanguageKey.BAD_EQUIPMENT), lastpacket?.Message);
        }

        [TestMethod]
        public async Task Test_BadFairyAsync()
        {
            UseItem.Mode = 1;
            Session!.Character.UseSp = true;
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(2), Session.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            var sp = InventoryItemInstance.Create(_itemProvider.Create(4), Session.Character.CharacterId);
            Session.Character.InventoryService.AddItemToPocket(sp, NoscorePocketType.Wear);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (MsgPacket?)Session.LastPackets.FirstOrDefault(s => s is MsgPacket);
            Assert.AreEqual(GameLanguage.Instance.GetMessageFromKey(LanguageKey.BAD_FAIRY,
                    Session.Account.Language), lastpacket?.Message);
        }

        [TestMethod]
        public async Task Test_SpLoadingAsync()
        { 
            UseItem.Mode = 1;
            SystemTime.Freeze();
            Session!.Character.LastSp = SystemTime.Now();
            Session.Character.SpCooldown = 300;
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(4), Session.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            var sp = InventoryItemInstance.Create(_itemProvider.Create(4), Session.Character.CharacterId);
            Session.Character.InventoryService.AddItemToPocket(sp, NoscorePocketType.Wear);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (MsgPacket?)Session.LastPackets.FirstOrDefault(s => s is MsgPacket);
            Assert.AreEqual(string.Format(GameLanguage.Instance.GetMessageFromKey(LanguageKey.SP_INLOADING,
                    Session.Account.Language),
                Session.Character.SpCooldown), lastpacket?.Message);
        }

        [TestMethod]
        public async Task Test_UseSpAsync()
        {
            UseItem.Mode = 1;
            Session!.Character.UseSp = true;
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(4), Session.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            var sp = InventoryItemInstance.Create(_itemProvider.Create(4), Session.Character.CharacterId);
            Session.Character.InventoryService.AddItemToPocket(sp, NoscorePocketType.Wear);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (SayPacket?)Session.LastPackets.FirstOrDefault(s => s is SayPacket);
            Assert.AreEqual(
                GameLanguage.Instance.GetMessageFromKey(LanguageKey.SP_BLOCKED, Session.Account.Language), 
                lastpacket?.Message);
        }

        [TestMethod]
        public async Task Test_UseDestroyedSpAsync()
        {
            UseItem.Mode = 1;
            SystemTime.Freeze();
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(4), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            itemInstance.ItemInstance!.Rare = -2;
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (MsgPacket?)Session.LastPackets.FirstOrDefault(s => s is MsgPacket);
            Assert.AreEqual(GameLanguage.Instance.GetMessageFromKey(LanguageKey.CANT_EQUIP_DESTROYED_SP,
                    Session.Account.Language), lastpacket?.Message);
        }

        [TestMethod]
        public async Task Test_Use_BadJobLevelAsync()
        {
            UseItem.Mode = 1;
            SystemTime.Freeze();
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(6), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (SayPacket?)Session.LastPackets.FirstOrDefault(s => s is SayPacket);
            Assert.AreEqual(GameLanguage.Instance.GetMessageFromKey(LanguageKey.LOW_JOB_LVL,
                    Session.Account.Language), lastpacket?.Message);
        }

        [TestMethod]
        public async Task Test_Use_SPAsync()
        {
            UseItem.Mode = 1;
            SystemTime.Freeze();
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(4), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (SpPacket?)Session.LastPackets.FirstOrDefault(s => s is SpPacket);
            Assert.IsNotNull(lastpacket);
        }

        [TestMethod]
        public async Task Test_Use_FairyAsync()
        {
            UseItem.Mode = 1;
            SystemTime.Freeze();
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(2), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (PairyPacket)Session.Character.MapInstance.LastPackets.FirstOrDefault(s => s is PairyPacket);
            Assert.IsNotNull(lastpacket);
        }

        [TestMethod]
        public async Task Test_Use_AmuletAsync()
        {
            UseItem.Mode = 1;
            SystemTime.Freeze();
            var itemInstance = InventoryItemInstance.Create(_itemProvider!.Create(7), Session!.Character.CharacterId);
            Session.Character.InventoryService!.AddItemToPocket(itemInstance);
            await ExecuteInventoryItemInstanceEventHandlerAsync(itemInstance).ConfigureAwait(false);
            var lastpacket = (EffectPacket?)Session.LastPackets.FirstOrDefault(s => s is EffectPacket);
            Assert.IsNotNull(lastpacket);
            Assert.AreEqual(SystemTime.Now().AddSeconds(itemInstance.ItemInstance!.Item!.ItemValidTime), itemInstance.ItemInstance.ItemDeleteTime);
        }
    }
}