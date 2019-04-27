using SDL2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Chip8
{
    class Program
    {
        static void Main(string[] args)
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
            {
                Console.WriteLine("SDL failed to init.");
                return;
            }

            IntPtr window = SDL.SDL_CreateWindow("Chip-8 Interpreter", 128, 128, 64 * 8, 32 * 8, 0);

            if (window == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a window.");
                return;
            }

            IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            if (renderer == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a valid renderer.");
                return;
            }

            CPU cpu = new CPU();

            using (BinaryReader reader = new BinaryReader(new FileStream("../../sample.ch8", FileMode.Open)))
            {
                List<byte> program = new List<byte>();

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    program.Add(reader.ReadByte());
                }

                cpu.LoadProgram(program.ToArray());
            }

            SDL.SDL_Event sdlEvent;
            bool running = true;

            int sample = 0;
            int beepSamples = 0;

            SDL.SDL_AudioSpec audioSpec = new SDL.SDL_AudioSpec();
            audioSpec.channels = 1;
            audioSpec.freq = 44100;
            audioSpec.samples = 256;
            audioSpec.format = SDL.AUDIO_S8;
            audioSpec.callback = new SDL.SDL_AudioCallback((userdata, stream, length) =>
            {
                if (cpu == null) return;

                sbyte[] waveData = new sbyte[length];

                for (int i = 0; i < waveData.Length && cpu.SoundTimer > 0; i++, beepSamples++)
                {
                    if (beepSamples == 730)
                    {
                        beepSamples = 0;
                        cpu.SoundTimer--;
                    }

                    waveData[i] = (sbyte)(127 * Math.Sin(sample * Math.PI * 2 * 604.1 / 44100));
                    sample++;
                }

                byte[] byteData = (byte[])(Array)waveData;

                Marshal.Copy(byteData, 0, stream, byteData.Length);
            });

            SDL.SDL_OpenAudio(ref audioSpec, IntPtr.Zero);
            SDL.SDL_PauseAudio(0);

            IntPtr sdlSurface, sdlTexture = IntPtr.Zero;
            Stopwatch frameTimer = Stopwatch.StartNew();
            int ticksPer60hz = (int)(Stopwatch.Frequency * 0.016);

            while (running)
            {
                try
                {
                    if (!cpu.WaitingForKeyPress) cpu.Step();

                    if (frameTimer.ElapsedTicks > ticksPer60hz)
                    {
                        while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                        {
                            if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                            {
                                running = false;
                            }
                            else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN)
                            {
                                var key = KeyCodeToKey((int)sdlEvent.key.keysym.sym);
                                cpu.Keyboard |= (ushort)(1 << key);

                                if (cpu.WaitingForKeyPress) cpu.KeyPressed((byte)key);
                            }
                            else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYUP)
                            {
                                var key = KeyCodeToKey((int)sdlEvent.key.keysym.sym);
                                cpu.Keyboard &= (ushort)~(1 << key);
                            }
                        }

                        var displayHandle = GCHandle.Alloc(cpu.Display, GCHandleType.Pinned);

                        if (sdlTexture != IntPtr.Zero) SDL.SDL_DestroyTexture(sdlTexture);

                        sdlSurface = SDL.SDL_CreateRGBSurfaceFrom(displayHandle.AddrOfPinnedObject(), 64, 32, 32, 64 * 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
                        sdlTexture = SDL.SDL_CreateTextureFromSurface(renderer, sdlSurface);

                        displayHandle.Free();

                        SDL.SDL_RenderClear(renderer);
                        SDL.SDL_RenderCopy(renderer, sdlTexture, IntPtr.Zero, IntPtr.Zero);
                        SDL.SDL_RenderPresent(renderer);

                        frameTimer.Restart();
                    }

                    Thread.Sleep(1);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            SDL.SDL_DestroyRenderer(renderer);
            SDL.SDL_DestroyWindow(window);
        }

        private static int KeyCodeToKey(int keycode)
        {
            int keyIndex = 0;
            if (keycode < 58) keyIndex = keycode - 48;
            else keyIndex = keycode - 87;

            return keyIndex;
        }
    }
}
