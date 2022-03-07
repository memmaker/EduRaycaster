using System;

namespace MonoGamePlayground
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new Raycaster())
                game.Run();
        }
    }
}
