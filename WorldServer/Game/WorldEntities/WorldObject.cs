﻿/*
 * Copyright (C) 2012 Arctium <http://>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using Framework.Constants;
using Framework.Network.Packets;
using Framework.ObjectDefines;
using System;
using System.Collections;
using WorldServer.Game.Managers;
using WorldServer.Game.Spawns;
using WorldServer.Network;

namespace WorldServer.Game.WorldEntities
{
    public class WorldObject
    {
        // General object data
        public UInt64 Guid;
        public Vector4 Position;
        public UInt32 Map;

        // Some data
        public UInt64 TargetGuid;

        public bool IsInWorld { get; set; }
        public uint Blocks;
        public byte[] Mask;
        public int DataLength;
        public Hashtable UpdateData = new Hashtable();

        public WorldObject() { }
        
        public WorldObject(int dataLength)
        {
            IsInWorld = false;
            DataLength = dataLength;
            Blocks = (uint)(DataLength + 31) / 32;
            Mask = new byte[(Blocks * 4) << 2];
        }

        public void SetUpdateField<T>(int index, T value, byte offset = 0)
        {
            switch (value.GetType().Name)
            {
                case "Byte":
                case "UInt16":
                {
                    SetBit(index);

                    if (UpdateData.ContainsKey(index))
                        UpdateData[index] = (uint)((uint)UpdateData[index] | (uint)((uint)Convert.ChangeType(value, typeof(uint)) << (offset * (value.GetType().Name == "Byte" ? 8 : 16))));
                    else
                        UpdateData[index] = (uint)((uint)Convert.ChangeType(value, typeof(uint)) << (offset * (value.GetType().Name == "Byte" ? 8 : 16)));

                    break;
                }
                case "UInt64":
                {
                    SetBit(index);
                    SetBit(index + 1);

                    ulong tmpValue = (ulong)Convert.ChangeType(value, typeof(ulong));

                    UpdateData[index] = (uint)(tmpValue & UInt32.MaxValue);
                    UpdateData[index + 1] = (uint)((tmpValue >> 32) & UInt32.MaxValue);
                    
                    break;
                }
                case "Int32":
                case "UInt32":
                case "Single":
                default:
                {
                    SetBit(index);
                    UpdateData[index] = value;

                    break;
                }
            }
        }

        public void WriteUpdateFields(ref PacketWriter packet, bool sendAllFields = true)
        {
            packet.WriteUInt8((byte)Blocks);
            packet.Write(Mask, 0, (int)Blocks * 4);

            for (int i = 0; i < DataLength; i++)
            {
                if (GetBit(i))
                {
                    try
                    {
                        switch (UpdateData[i].GetType().Name)
                        {
                            /*case "Int16":
                                packet.WriteInt16((short)UpdateData[i]);
                                break;
                            case "UInt16":
                                packet.WriteUInt16((ushort)UpdateData[i]);
                                break;*/
                            case "Int32":
                                packet.WriteInt32((int)UpdateData[i]);
                                break;
                            case "UInt32":
                                packet.WriteUInt32((uint)UpdateData[i]);
                                break;
                            case "Single":
                                packet.WriteFloat((float)UpdateData[i]);
                                break;
                            default:
                                packet.WriteInt32((int)UpdateData[i]);
                                break;
                        }
                    }
                    catch
                    {
                        if (sendAllFields)
                            packet.WriteInt32(0);
                    }
                }
            }
        }

        public void WriteDynamicUpdateFields(ref PacketWriter packet)
        {
            packet.WriteUInt8(0);
        }

        public void SetBit(int index)
        {
            Mask[index >> 3] |= (byte)(1 << (index & 0x7));
        }

        public bool GetBit(int index)
        {
            return (Mask[index >> 3] & 1 << (index & 0x7)) != 0;
        }

        public void AddSpawnsToWorld(ref WorldClass session)
        {
            var pChar = session.Character;

            UpdateFlag updateFlags = UpdateFlag.Alive | UpdateFlag.Rotation;

            if (Globals.SpawnMgr.CreatureSpawns.Count > 0)
            {

                foreach (var s in Globals.SpawnMgr.CreatureSpawns)
                {
                    WorldObject spawn = s.Key as CreatureSpawn;
                    spawn.ToCreature().SetCreatureFields();

                    var data = s.Value as Creature;

                    if (spawn.Map != pChar.Map)
                        continue;

                    PacketWriter updateObject = new PacketWriter(LegacyMessage.UpdateObject);

                    updateObject.WriteUInt16((ushort)spawn.Map);
                    updateObject.WriteUInt32(1);
                    updateObject.WriteUInt8(1);
                    updateObject.WriteGuid(spawn.Guid);
                    updateObject.WriteUInt8(3);

                    Globals.WorldMgr.WriteUpdateObjectMovement(ref updateObject, ref spawn, updateFlags);

                    spawn.WriteUpdateFields(ref updateObject);
                    spawn.WriteDynamicUpdateFields(ref updateObject);

                    session.Send(updateObject);
                }
            }
        }

        public void RemoveFromWorld()
        {

        }

        public Character ToCharacter()
        {
            return this as Character;
        }

        public CreatureSpawn ToCreature()
        {
            return this as CreatureSpawn;
        }

    }
}
