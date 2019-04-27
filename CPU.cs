using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Chip8
{
    public class CPU
    {
        public byte[] RAM = new byte[4096];
        public byte[] V = new byte[16];
        public ushort PC = 0;
        public ushort I = 0;
        public Stack<ushort> Stack = new Stack<ushort>();
        public byte DelayTimer;
        public byte SoundTimer;
        public ushort Keyboard;

        public uint[] Display = new uint[64 * 32];

        private Random generator = new Random(Environment.TickCount);

        public bool WaitingForKeyPress = false;

        private void InitializeFont()
        {
            byte[] characters = new byte[] { 0xF0, 0x90, 0x90, 0x90, 0xF0, 0x20, 0x60, 0x20, 0x20, 0x70, 0xF0, 0x10, 0xF0, 0x80, 0xF0, 0xF0, 0x10, 0xF0, 0x10, 0xF0, 0x90, 0x90, 0xF0, 0x10, 0x10, 0xF0, 0x80, 0xF0, 0x10, 0xF0, 0xF0, 0x80, 0xF0, 0x90, 0xF0, 0xF0, 0x10, 0x20, 0x40, 0x40, 0xF0, 0x90, 0xF0, 0x90, 0xF0, 0xF0, 0x90, 0xF0, 0x10, 0xF0, 0xF0, 0x90, 0xF0, 0x90, 0x90, 0xE0, 0x90, 0xE0, 0x90, 0xE0, 0xF0, 0x80, 0x80, 0x80, 0xF0, 0xE0, 0x90, 0x90, 0x90, 0xE0, 0xF0, 0x80, 0xF0, 0x80, 0xF0, 0xF0, 0x80, 0xF0, 0x80, 0x80 };
            Array.Copy(characters, RAM, characters.Length);
        }

        public void LoadProgram(byte[] program)
        {
            RAM = new byte[4096];
            InitializeFont();
            for (int i = 0; i < program.Length; i++)
            {
                RAM[512 + i] = program[i];
            }
            PC = 512;
        }

        private Stopwatch watch = new Stopwatch();

        public void KeyPressed(byte key)
        {
            WaitingForKeyPress = false;

            var opcode = (ushort)((RAM[PC] << 8) | RAM[PC + 1]);
            V[(opcode & 0x0F00) >> 8] = key;
            PC += 2;
        }

        private int ticksPer60hz = (int)(Stopwatch.Frequency * 0.016);

        public void Step()
        {
            if (!watch.IsRunning) watch.Start();

            if (DelayTimer > 0)
            {
                if (watch.ElapsedTicks > ticksPer60hz)
                {
                    DelayTimer--;
                    watch.Restart();
                }
            }

            var opcode = (ushort)((RAM[PC] << 8) | RAM[PC + 1]);

            if (WaitingForKeyPress)
            {
                throw new Exception("Do not call Step when WaitingForKeyPress is set.");
            }

            ushort nibble = (ushort)(opcode & 0xF000);

            PC += 2;

            switch (nibble)
            {
                case 0x0000:
                    if (opcode == 0x00e0)
                    {
                        for (int i = 0; i < Display.Length; i++) Display[i] = 0;
                    }
                    else if (opcode == 0x00ee)
                    {
                        PC = Stack.Pop();
                    }
                    else
                    {
                        throw new Exception($"Unsupported opcode {opcode.ToString("X4")}");
                    }
                    break;
                case 0x1000:
                    PC = (ushort)(opcode & 0x0FFF);
                    break;
                case 0x2000:
                    Stack.Push(PC);
                    PC = (ushort)(opcode & 0x0FFF);
                    break;
                case 0x3000:
                    if (V[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF)) PC += 2;
                    break;
                case 0x4000:
                    if (V[(opcode & 0x0F00) >> 8] != (opcode & 0x00FF)) PC += 2;
                    break;
                case 0x5000:
                    if (V[(opcode & 0x0F00) >> 8] == V[(opcode & 0x00F0) >> 4]) PC += 2;
                    break;
                case 0x6000:
                    V[(opcode & 0x0F00) >> 8] = (byte)(opcode & 0x00FF);
                    break;
                case 0x7000:
                    V[(opcode & 0x0F00) >> 8] += (byte)(opcode & 0x00FF);
                    break;
                case 0x8000:
                    int vx = (opcode & 0x0F00) >> 8;
                    int vy = (opcode & 0x00F0) >> 4;
                    switch (opcode & 0x000F)
                    {
                        case 0: V[vx] = V[vy]; break;
                        case 1: V[vx] = (byte)(V[vx] | V[vy]); break;
                        case 2: V[vx] = (byte)(V[vx] & V[vy]); break;
                        case 3: V[vx] = (byte)(V[vx] ^ V[vy]); break;
                        case 4:
                            V[15] = (byte)(V[vx] + V[vy] > 255 ? 1 : 0);
                            V[vx] = (byte)((V[vx] + V[vy]) & 0x00FF);
                            break;
                        case 5:
                            V[15] = (byte)(V[vx] > V[vy] ? 1 : 0);
                            V[vx] = (byte)((V[vx] - V[vy]) & 0x00FF);
                            break;
                        case 6:
                            V[15] = (byte)(V[vx] & 0x0001);
                            V[vx] = (byte)(V[vx] >> 1);
                            break;
                        case 7:
                            V[15] = (byte)(V[vy] > V[vx] ? 1 : 0);
                            V[vx] = (byte)((V[vy] - V[vx]) & 0x00FF);
                            break;
                        case 14:
                            V[15] = (byte)(((V[vx] & 0x80) == 0x80) ? 1 : 0);
                            V[vx] = (byte)(V[vx] << 1);
                            break;
                        default:
                            throw new Exception($"Unsupported opcode {opcode.ToString("X4")}");
                    }
                    break;
                case 0x9000:
                    if (V[(opcode & 0x0F00) >> 8] != V[(opcode & 0x00F0) >> 4]) PC += 2;
                    break;
                case 0xA000:
                    I = (ushort)(opcode & 0x0FFF);
                    break;
                case 0xB000:
                    PC = (ushort)((opcode & 0x0FFF) + V[0]);
                    break;
                case 0xC000:
                    V[(opcode & 0x0F00) >> 8] = (byte)(generator.Next() & (opcode & 0x00FF));
                    break;
                case 0xD000:
                    int x = V[(opcode & 0x0F00) >> 8];
                    int y = V[(opcode & 0x00F0) >> 4];
                    int n = opcode & 0x000F;

                    V[15] = 0;

                    for (int i = 0; i < n; i++)
                    {
                        byte mem = RAM[I + i];

                        for (int j = 0; j < 8; j++)
                        {
                            byte pixel = (byte)((mem >> (7 - j)) & 0x01);
                            int index = x + j + (y + i) * 64;

                            if (index > 2047) continue;

                            if (pixel == 1 && Display[index] != 0) V[15] = 1;

                            Display[index] = (Display[index] != 0 && pixel == 0) || (Display[index] == 0 && pixel == 1) ? 0xffffffff : 0;//(byte)(Display[index] ^ pixel);
                        }
                    }
                    break;
                case 0xE000:
                    if ((opcode & 0x00FF) == 0x009E)
                    {
                        if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) == 0x01) PC += 2;
                        break;
                    }
                    else if ((opcode & 0x00FF) == 0x00A1)
                    {
                        if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) != 0x01) PC += 2;
                        break;
                    }
                    else throw new Exception($"Unsupported opcode {opcode.ToString("X4")}");
                case 0xF000:
                    int tx = (opcode & 0x0F00) >> 8;

                    switch (opcode & 0x00FF)
                    {
                        case 0x07:
                            V[tx] = DelayTimer;
                            break;
                        case 0x0A:
                            WaitingForKeyPress = true;
                            PC -= 2;
                            break;
                        case 0x15:
                            DelayTimer = V[tx];
                            break;
                        case 0x18:
                            SoundTimer = V[tx];
                            break;
                        case 0x1E:
                            I = (ushort)(I + V[tx]);
                            break;
                        case 0x29:
                            I = (ushort)(V[tx] * 5);
                            break;
                        case 0x33:
                            RAM[I] = (byte)(V[tx] / 100);
                            RAM[I + 1] = (byte)((V[tx] % 100) / 10);
                            RAM[I + 2] = (byte)(V[tx] % 10);
                            break;
                        case 0x55:
                            for (int i = 0; i <= tx; i++)
                            {
                                RAM[I + i] = V[i];
                            }
                            break;
                        case 0x65:
                            for (int i = 0; i <= tx; i++)
                            {
                                V[i] = RAM[I + i];
                            }
                            break;
                        default:
                            throw new Exception($"Unsupported opcode {opcode.ToString("X4")}");
                    }
                    break;
                default:
                    throw new Exception($"Unsupported opcode {opcode.ToString("X4")}");
            }
        }
    }
}
