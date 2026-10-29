using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// One stack in the party's pack (the original's items(n), Inventory.vb): what a picked-up pickup became, and how many
    /// of it the pack holds. Stacks are by name, as checkItemPickup matched them.
    /// </summary>
    public sealed class InventoryItem
    {
        internal InventoryItem(PickupDefinition source, string texture)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Texture = texture;
            Amount = 1;
        }

        /// <summary>The pickup the stack was first made from (its name, script and texts).</summary>
        public PickupDefinition Source { get; }

        public string Name => Source.Name;
        public string Script => Source.Script;
        public string Description => Source.Description;
        public string Effect => Source.Effect;
        public float Weight => Source.Weight;
        public ItemType Type => Source.Type;
        public int Value => Source.Value;

        /// <summary>The inventory icon's picture file (the potato and turnip have their own; the rest show their world picture).</summary>
        public string Texture { get; }

        public int Amount { get; internal set; }

        public override string ToString() => Name + (Amount > 1 ? " x " + Amount : "");
    }

    /// <summary>
    /// The party's pack and purse (Inventory.vb's items, itemCount and inventoryWeight; Root.Gold): one list of stacks
    /// for the whole party, the total weight carried, and the gold. Picking up (U.OVW-US-A12) fills it; the
    /// <see cref="InventoryControl"/> (U.OVW-US-A13) shows, swaps, uses and drops from it.
    /// </summary>
    public sealed class Inventory
    {
        readonly List<InventoryItem> items = new List<InventoryItem>();

        /// <summary>The stacks, in the order they were first picked up.</summary>
        public IReadOnlyList<InventoryItem> Items => items;

        /// <summary>The weight the party carries (inventoryWeight).</summary>
        public float Weight { get; private set; }

        /// <summary>The party's gold (Root.Gold): what the gold and satchels pay out.</summary>
        public int Gold { get; internal set; }

        /// <summary>The stack of that name, or null.</summary>
        public InventoryItem Find(string name)
        {
            foreach (var item in items)
                if (item.Name == name) return item;
            return null;
        }

        /// <summary>
        /// checkItemPickup's add: a pickup of a name already carried adds one to that stack and its weight to the load;
        /// otherwise it takes a new stack of one (with its inventory icon). Returns the stack.
        /// </summary>
        internal InventoryItem Add(PickupDefinition pickup, string texture)
        {
            var stack = Find(pickup.Name);
            if (stack != null)
            {
                stack.Amount++;
                Weight += stack.Weight;
                return stack;
            }

            stack = new InventoryItem(pickup, texture);
            items.Add(stack);
            Weight += stack.Weight * stack.Amount;
            return stack;
        }

        /// <summary>
        /// One of the stack at <paramref name="slot"/> leaves the pack (a use or a drop): a stack of more loses one and its
        /// weight; a stack of one is removed (RemoveFromInventory), the stacks after it closing up. True when the stack was removed.
        /// </summary>
        internal bool TakeOne(int slot)
        {
            var stack = items[slot];
            if (stack.Amount > 1)
            {
                stack.Amount--;
                Weight -= stack.Weight;
                return false;
            }

            Weight -= stack.Weight;
            items.RemoveAt(slot);
            return true;
        }

        /// <summary>SwapItem: the stacks at <paramref name="a"/> and <paramref name="b"/> change places.</summary>
        internal void Swap(int a, int b)
        {
            var t = items[a];
            items[a] = items[b];
            items[b] = t;
        }
    }
}
