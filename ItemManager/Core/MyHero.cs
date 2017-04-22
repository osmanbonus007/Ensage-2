namespace ItemManager.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Abilities;
    using Abilities.Base;
    using Abilities.Interfaces;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.Common.Objects.UtilityObjects;

    using SharpDX;

    using Utils;

    internal class MyHero : IDisposable
    {
        private readonly MultiSleeper disabledItems = new MultiSleeper();

        private readonly Dictionary<Item, ItemSlot> itemSlots = new Dictionary<Item, ItemSlot>();

        public MyHero(Hero hero)
        {
            Hero = hero;
            Player = hero.Player;
            Handle = hero.Handle;
            Team = hero.Team;
            EnemyTeam = hero.GetEnemyTeam();
        }

        public List<Ability> Abilities { get; } = new List<Ability>();

        public float Damage => Hero.MinimumDamage + Hero.BonusDamage;

        public List<Item> DroppedItems { get; } = new List<Item>();

        public Team EnemyTeam { get; }

        public uint Handle { get; }

        public float Health => Hero.Health;

        public float HealthPercentage => Health / MaximumHealth * 100;

        public Hero Hero { get; }

        public Inventory Inventory => Hero.Inventory;

        public bool IsAlive => Hero.IsAlive;

        public List<Item> Items { get; } = new List<Item>();

        public float Mana => Hero.Mana;

        public float ManaPercentage => Mana / MaximumMana * 100;

        public float MaximumHealth => Hero.MaximumHealth;

        public float MaximumMana => Hero.MaximumMana;

        public float MissingHealth => MaximumHealth - Health;

        public float MissingMana => MaximumMana - Mana;

        public Player Player { get; }

        public Vector3 Position => Hero.Position;

        public Team Team { get; }

        public List<UsableAbility> UsableAbilities { get; } = new List<UsableAbility>();

        public int BuybackCost()
        {
            return (int)(100 + Math.Pow(Hero.Level, 2) * 1.5 + Game.GameTime / 60 * 15);
        }

        public bool CanAttack()
        {
            return Hero.IsAlive && !Hero.IsChanneling() && Hero.CanAttack();
        }

        public bool CanUseAbilities()
        {
            return CanUse() && Hero.CanCast();
        }

        public bool CanUseItems()
        {
            return CanUse() && Hero.CanUseItems();
        }

        public void Dispose()
        {
            DroppedItems.Clear();
            UsableAbilities.Clear();
            Abilities.Clear();
            Items.Clear();
            itemSlots.Clear();
        }

        public float Distance2D(Entity entity)
        {
            return Hero.Distance2D(entity);
        }

        public float Distance2D(Vector3 position)
        {
            return Hero.Distance2D(position);
        }

        public void DropItem(Item item, ItemStoredPlace itemStored = ItemStoredPlace.Any, bool saveSlot = true)
        {
            if (saveSlot)
            {
                SaveItemSlot(item, itemStored);
            }

            Hero.DropItem(item, Hero.Position, true);
            DroppedItems.Add(item);
        }

        public void DropItems(ItemStats dropItemStats, bool toBackpack = false, params IRecoveryAbility[] ignoredItems)
        {
            foreach (var item in GetMyItems(ItemStoredPlace.Inventory)
                .Where(
                    x => ignoredItems.All(z => z.Handle != x.Handle) && !DroppedItems.Contains(x)
                         && !disabledItems.Sleeping(x.Handle) && x.IsEnabled && x.IsDroppable
                         && x.GetItemStats().HasFlag(dropItemStats)))
            {
                if (toBackpack && ItemsCanBeDisabled())
                {
                    var slot = GetSlot(item.Handle, ItemStoredPlace.Inventory);
                    item.MoveItem(ItemSlot.BackPack_1);
                    disabledItems.Sleep(6000, item.Handle);
                    UsableAbilities.FirstOrDefault(x => x.Handle == item.Handle)?.SetSleep(6000);

                    if (slot != null)
                    {
                        item.MoveItem(slot.Value);
                    }
                }
                else
                {
                    DropItem(item, ItemStoredPlace.Inventory);
                }
            }
        }

        public Item GetBestBackpackItem()
        {
            return GetMyItems(ItemStoredPlace.Backpack)
                .Where(x => !x.IsRecipe && !x.IsEmptyBottle())
                .OrderByDescending(x => x.Cost)
                .FirstOrDefault();
        }

        public uint GetItemCharges(AbilityId id)
        {
            var wards = 0u;

            switch (id)
            {
                case AbilityId.item_ward_observer:
                {
                    wards = (uint)Items.Where(x => x.Id == AbilityId.item_ward_dispenser).Sum(x => x.CurrentCharges);
                    break;
                }
                case AbilityId.item_ward_sentry:
                {
                    wards = (uint)Items.Where(x => x.Id == AbilityId.item_ward_dispenser).Sum(x => x.SecondaryCharges);
                    break;
                }
            }

            return wards + (uint)Items.Where(x => x.Id == id).Sum(x => x.CurrentCharges);
        }

        public IEnumerable<Item> GetMyItems(ItemStoredPlace itemStoredPlace)
        {
            var list = new List<Item>();

            if (itemStoredPlace.HasFlag(ItemStoredPlace.Inventory))
            {
                list.AddRange(Hero.Inventory.Items);
            }

            if (itemStoredPlace.HasFlag(ItemStoredPlace.Backpack))
            {
                list.AddRange(Hero.Inventory.Backpack);
            }

            if (itemStoredPlace.HasFlag(ItemStoredPlace.Stash))
            {
                list.AddRange(Hero.Inventory.Stash);
            }

            return list;
        }

        public ItemSlot? GetSavedSlot(Item item)
        {
            return itemSlots.FirstOrDefault(x => x.Key.Equals(item)).Value;
        }

        public ItemSlot? GetSavedSlot(uint handle)
        {
            return itemSlots.FirstOrDefault(x => x.Key.Handle == handle).Value;
        }

        public ItemSlot? GetSlot(AbilityId abilityId, ItemStoredPlace itemStoredPlace)
        {
            return GetSlot(null, abilityId, itemStoredPlace);
        }

        public ItemSlot? GetSlot(uint handle, ItemStoredPlace itemStoredPlace)
        {
            return GetSlot(handle, null, itemStoredPlace);
        }

        public bool HasModifier(string modifierName)
        {
            return Hero.HasModifier(modifierName);
        }

        public bool IsAtBase()
        {
            return Hero.ActiveShop == ShopType.Base;
        }

        public bool IsInvisible()
        {
            return Hero.IsInvisible() && !Hero.IsVisibleToEnemies;
        }

        public bool ItemsCanBeDisabled()
        {
            return Hero.ActiveShop == ShopType.None;
        }

        public float PickUpItems()
        {
            var bottle = UsableAbilities.FirstOrDefault(x => x.Id == AbilityId.item_bottle) as Bottle;
            if (bottle != null && bottle.TookFromStash)
            {
                var slot = GetSavedSlot(bottle.Handle);
                if (slot != null)
                {
                    bottle.MoveItem(slot.Value, false);
                }
            }

            if (!DroppedItems.Any(x => x != null && x.IsValid && x.IsVisible))
            {
                return 0;
            }

            var items = ObjectManager.GetEntitiesParallel<PhysicalItem>()
                .Where(x => x.IsVisible && x.Distance2D(Hero) < 800 && DroppedItems.Contains(x.Item))
                .Reverse();

            if (!items.Any())
            {
                return 0;
            }

            Hero.Stop();

            var sleep = DroppedItems.Count * Game.Ping;

            foreach (var physicalItem in items)
            {
                if (Hero.PickUpItem(physicalItem, true))
                {
                    DroppedItems.Remove(physicalItem.Item);

                    var slot = GetSavedSlot(physicalItem.Item);
                    var item = Items.FirstOrDefault(x => x.Handle == physicalItem.Item.Handle);

                    if (slot != null && item != null)
                    {
                        DelayAction.Add(200, () => { item.MoveItem(slot.Value); });
                    }
                }
            }

            return sleep;
        }

        public float RespawnTime()
        {
            return (float)3.8 * Hero.Level + 5 + Hero.RespawnTimePenalty;
        }

        public void SaveItemSlot(Item item, ItemStoredPlace itemStored = ItemStoredPlace.Any)
        {
            var slot = GetSlot(item.Handle, itemStored);
            if (slot != null)
            {
                itemSlots[item] = slot.Value;
            }
        }

        private bool CanUse()
        {
            return Hero.IsAlive && !Hero.IsChanneling() && (!IsInvisible() || CanUseAbilitiesInInvisibility());
        }

        private bool CanUseAbilitiesInInvisibility()
        {
            if (Hero.HeroId == HeroId.npc_dota_hero_riki)
            {
                return true;
            }

            if (Hero.HeroId == HeroId.npc_dota_hero_treant && Hero.HasModifier("modifier_treant_natures_guise_invis"))
            {
                return true;
            }

            return false;
        }

        private ItemSlot? GetSlot(uint? handle, AbilityId? abilityId, ItemStoredPlace itemStoredPlace)
        {
            var start = ItemSlot.InventorySlot_1;
            var end = ItemSlot.StashSlot_6;

            switch (itemStoredPlace)
            {
                case ItemStoredPlace.Inventory:
                {
                    end = ItemSlot.InventorySlot_6;
                    break;
                }
                case ItemStoredPlace.Backpack:
                {
                    start = ItemSlot.BackPack_1;
                    end = ItemSlot.BackPack_3;
                    break;
                }
                case ItemStoredPlace.Stash:
                {
                    start = ItemSlot.StashSlot_1;
                    break;
                }
            }

            for (var i = start; i <= end; i++)
            {
                var currentItem = Inventory.GetItem(i);
                if (currentItem != null && (abilityId != null && currentItem.Id == abilityId.Value
                                            || handle != null && currentItem.Handle == handle.Value))
                {
                    return i;
                }
            }

            return null;
        }
    }
}