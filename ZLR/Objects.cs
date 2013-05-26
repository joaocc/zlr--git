using System;

namespace ZLR.VM
{
    public partial class ZMachine
    {
        private short GetPropAddr(ushort obj, short prop)
        {
            if (obj == 0)
                return 0;

            if (zversion <= 3)
            {
                int propTable = (ushort)GetWord(GetObjectAddress(obj) + 7);

                // skip object name
                propTable += 2 * GetByte(propTable) + 1;

                int addr = propTable;
                byte b = GetByte(addr++);
                while (b != 0)
                {
                    int num = b & 31;
                    int len = (b >> 5) + 1;

                    if (num == prop)
                        return (short)addr;
                    else if (num < prop)
                        break;

                    addr += len;
                    b = GetByte(addr++);
                }
            }
            else
            {
                int propTable = (ushort)GetWord(GetObjectAddress(obj) + 12);

                // skip object name
                propTable += 2 * GetByte(propTable) + 1;

                int addr = propTable;
                byte b = GetByte(addr++);
                while (b != 0)
                {
                    int num = b & 63;
                    int len;
                    if ((b & 128) == 0)
                    {
                        if ((b & 64) == 0)
                            len = 1;
                        else
                            len = 2;
                    }
                    else
                    {
                        b = GetByte(addr++);
                        System.Diagnostics.Debug.Assert((b & 128) == 128);
                        len = b & 63;
                        if (len == 0)
                            len = 64;
                    }

                    if (num == prop)
                        return (short)addr;
                    else if (num < prop)
                        break;

                    addr += len;
                    b = GetByte(addr++);
                }
            }

            return 0;
        }

#pragma warning disable 0169
        private short GetNextProp(ushort obj, short prop)
        {
            if (obj == 0)
                return 0;

            if (zversion <= 3)
            {
                int propTable = (ushort)GetWord(GetObjectAddress(obj) + 7);

                // skip object name
                propTable += 2 * GetByte(propTable) + 1;

                int addr = propTable;
                byte b = GetByte(addr++);
                while (b != 0)
                {
                    int num = b & 31;
                    int len = (b >> 5) + 1;

                    if (prop == 0 || num < prop)
                        return (short)num;

                    addr += len;
                    b = GetByte(addr++);
                }

            }
            else
            {
                int propTable = (ushort)GetWord(GetObjectAddress(obj) + 12);

                // skip object name
                propTable += 2 * GetByte(propTable) + 1;

                int addr = propTable;
                byte b = GetByte(addr++);
                while (b != 0)
                {
                    int num = b & 63;
                    int len;
                    if ((b & 128) == 0)
                    {
                        if ((b & 64) == 0)
                            len = 1;
                        else
                            len = 2;
                    }
                    else
                    {
                        b = GetByte(addr++);
                        System.Diagnostics.Debug.Assert((b & 128) == 128);
                        len = b & 63;
                        if (len == 0)
                            len = 64;
                    }

                    if (prop == 0 || num < prop)
                        return (short)num;

                    addr += len;
                    b = GetByte(addr++);
                }
            }

            return 0;
        }

        private short GetPropValue(ushort obj, short prop)
        {
            int addr = GetPropAddr(obj, prop);

            if (addr == 0)
                return GetWord(objectTable + 2 * (prop - 1));
            else
                return GetWord(addr);
        }

        private void SetPropValue(ushort obj, short prop, short value)
        {
            int addr = GetPropAddr(obj, prop);

            if (addr != 0)
            {
                short len = GetPropLength((ushort)addr);
                if (len == 1)
                    SetByte(addr, (byte)value);
                else
                    SetWord(addr, value);
            }
        }
#pragma warning restore 0169

        private short GetPropLength(ushort address)
        {
            if (address == 0)
                return 0;

            if (zversion <= 3)
            {
                byte b = GetByte(address - 1);
                return (short)((b >> 5) + 1);
            }
            else
            {
                byte b = GetByte(address - 1);
                if ((b & 128) == 0)
                {
                    if ((b & 64) == 0)
                        return 1;
                    else
                        return 2;
                }
                else
                {
                    short len = (short)(b & 63);
                    if (len == 0)
                        return 64;
                    else
                        return len;
                }
            }
        }

        private ushort GetObjectParent(ushort obj)
        {
            if (obj == 0)
                return 0;

            if (zversion <= 3)
                return (ushort)GetByte(GetObjectAddress(obj) + 4);

            return (ushort)GetWord(GetObjectAddress(obj) + 6);
        }

        private ushort GetObjectSibling(ushort obj)
        {
            if (obj == 0)
                return 0;

            if (zversion <= 3)
                return (ushort)GetByte(GetObjectAddress(obj) + 5);

            return (ushort)GetWord(GetObjectAddress(obj) + 8);
        }

        private ushort GetObjectChild(ushort obj)
        {
            if (obj == 0)
                return 0;

            if (zversion <= 3)
                return (ushort)GetByte(GetObjectAddress(obj) + 6);

            return (ushort)GetWord(GetObjectAddress(obj) + 10);
        }

        private void SetObjectParent(ushort obj, ushort value)
        {
            if (obj != 0)
            {
                if (zversion <= 3)
                    SetByte(GetObjectAddress(obj) + 4, (byte)value);
                else
                    SetWord(GetObjectAddress(obj) + 6, (short)value);
            }
        }

        private void SetObjectSibling(ushort obj, ushort value)
        {
            if (obj != 0)
            {
                if (zversion <= 3)
                    SetByte(GetObjectAddress(obj) + 5, (byte)value);
                else
                    SetWord(GetObjectAddress(obj) + 8, (short)value);
            }
        }

        private void SetObjectChild(ushort obj, ushort value)
        {
            if (obj != 0)
            {
                if (zversion <= 3)
                    SetByte(GetObjectAddress(obj) + 6, (byte)value);
                else
                    SetWord(GetObjectAddress(obj) + 10, (short)value);
            }
        }

#pragma warning disable 0169
        private void InsertObject(ushort obj, ushort dest)
        {
            if (obj == 0)
                return;

            ushort prevParent = GetObjectParent(obj);
            if (prevParent != 0)
            {
                ushort head = GetObjectChild(prevParent);
                if (head == obj)
                {
                    ushort prevSibling = GetObjectSibling(obj);
                    SetObjectChild(prevParent, prevSibling);
                }
                else
                {
                    ushort next = GetObjectSibling(head);
                    while (next != obj)
                    {
                        head = next;
                        next = GetObjectSibling(head);
                    }
                    SetObjectSibling(head, GetObjectSibling(obj));
                }
            }

            ushort prevChild = GetObjectChild(dest);
            SetObjectSibling(obj, prevChild);
            SetObjectParent(obj, dest);
            if (dest != 0)
                SetObjectChild(dest, obj);
        }
#pragma warning restore 0169

        private int GetObjectAddress(ushort obj)
        {
            if (zversion <= 3)
                return objectTable + 2 * 31 + 9 * (obj - 1);
            else
                return objectTable + 2 * 63 + 14 * (obj - 1);
        }

#pragma warning disable 0169
        private string GetObjectName(ushort obj)
        {
            if (obj == 0)
                return string.Empty;

            int propTable;
            if (zversion <= 3)
                propTable = (ushort)GetWord(GetObjectAddress(obj) + 7);
            else
                propTable = (ushort)GetWord(GetObjectAddress(obj) + 12);
            return DecodeString(propTable + 1);
        }

        private bool GetObjectAttr(ushort obj, short attr)
        {
            if (obj == 0)
                return false;

            int bit = 128 >> (attr & 7);
            int offset = attr >> 3;
            byte flags = GetByte(GetObjectAddress(obj) + offset);
            return (flags & bit) != 0;
        }

        private void SetObjectAttr(ushort obj, short attr, bool value)
        {
            if (obj == 0)
                return;

            int bit = 128 >> (attr & 7);
            int address = GetObjectAddress(obj) + (attr >> 3);
            byte flags = GetByte(address);
            if (value)
                flags |= (byte)bit;
            else
                flags &= (byte)~bit;
            SetByte(address, flags);
        }
#pragma warning restore 0169
    }
}