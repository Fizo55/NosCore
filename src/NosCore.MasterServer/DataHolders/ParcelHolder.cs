﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mapster;
using NosCore.Core;
using NosCore.Data;
using NosCore.Data.AliveEntities;
using NosCore.Data.WebApi;

namespace NosCore.MasterServer.DataHolders
{
    public class ParcelHolder : ConcurrentDictionary<long, ConcurrentDictionary<bool, ConcurrentDictionary<long, MailData>>>
    {
        private readonly IGenericDao<MailDto> _mailDao;
        private readonly List<ItemDto> _items;
        private readonly IGenericDao<CharacterDto> _characterDao;
        private readonly IGenericDao<IItemInstanceDto> _itemInstanceDao;

        public new ConcurrentDictionary<bool, ConcurrentDictionary<long, MailData>> this[long characterId]
        {
            // returns value if exists
            get
            {
                if (!ContainsKey(characterId))
                {
                    TryAdd(characterId, new ConcurrentDictionary<bool, ConcurrentDictionary<long, MailData>>());
                    this.First(s=>s.Key == characterId).Value.TryAdd(false, new ConcurrentDictionary<long, MailData>());
                    this.First(s => s.Key == characterId).Value.TryAdd(true, new ConcurrentDictionary<long, MailData>());
                }
                return this.First(s => s.Key == characterId).Value;
            }
        }
        public ParcelHolder(IGenericDao<CharacterDto> characterDao, IGenericDao<MailDto> mailDao, List<ItemDto> items, IGenericDao<IItemInstanceDto> itemInstanceDao)
        {
            _mailDao = mailDao;
            _items = items;
            _characterDao = characterDao;
            _itemInstanceDao = itemInstanceDao;
            Initialize();
        }

        private void Initialize()
        {
            var mails = _mailDao.LoadAll();
            var idcopy = 0;
            var idmail = 0;
            var charactersIds = Enumerable.Union(mails.Select(s => s.ReceiverId), mails.Where(s => s.SenderId != null).Select(s => (long)s.SenderId));
            var characternames = new Dictionary<long, string>();
            foreach (var characterId in charactersIds)
            {
                characternames.Add(characterId, _characterDao.FirstOrDefault(s => s.CharacterId == characterId).Name);
            }
            foreach (var mail in mails)
            {
                var itinst = _itemInstanceDao.FirstOrDefault(s => s.Id == mail.ItemInstanceId);
                var it = _items.FirstOrDefault(s => s.VNum == itinst.ItemVNum);
                var senderName = mail.SenderId == null ? "NOSMALL" : characternames[(long)mail.SenderId];
                var receiverName = characternames[mail.ReceiverId];
                var mailId = mail.IsSenderCopy ? (short)idcopy : (short)idmail;
                this[mail.IsSenderCopy ? (long)mail.SenderId : mail.ReceiverId][mail.IsSenderCopy].TryAdd(mailId,
                    new MailData
                    {
                        ItemInstance = itinst.Adapt<ItemInstanceDto>(),
                        SenderName = senderName,
                        ReceiverName = receiverName,
                        MailId = mailId,
                        MailDbKey = mail.MailId,
                        Title = mail.Title,
                        Date = mail.Date,
                        ItemType = (short)it.ItemType,
                        IsSenderCopy = mail.IsSenderCopy
                    });
                if (mail.IsSenderCopy)
                {
                    idcopy++;
                }
                else
                {
                    idmail++;
                }
            }
        }
    }
}
