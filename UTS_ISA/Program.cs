using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UTS_ISA
{
    /// <summary>
    /// Entry point aplikasi Secure Chat.
    ///
    /// ARSITEKTUR SINGLE EXE:
    ///   Aplikasi ini menggabungkan server + client dalam SATU file .exe.
    ///   Tidak ada chatServer.exe terpisah — server berjalan sebagai
    ///   background thread di dalam aplikasi yang sama.
    ///
    ///   Kenapa demikian?
    ///     Windows Application Control (AppLocker) memblokir .exe yang
    ///     tidak dikenal. Dengan menyatukan server ke dalam satu .exe,
    ///     kita menghindari masalah tersebut.
    ///
    ///   Cara kerja multi-instance:
    ///     - Instance PERTAMA yang dibuka → berhasil bind port 6000 → jadi SERVER
    ///     - Instance KEDUA, KETIGA, dst → port 6000 sudah dipakai → gagal bind
    ///       (error ditangkap diam-diam) → hanya jalan sebagai CLIENT
    ///     Semua instance client otomatis konek ke server di instance pertama.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Coba start server TCP di port 6000 sebagai background thread.
            // Kalau port sudah dipakai (instance lain sudah jadi server),
            // ServerHost.Start() akan gagal diam-diam dan app tetap jalan
            // sebagai client saja.
            ServerHost.Start();

            // Jalankan UI aplikasi mulai dari FormLogin
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormLogin());
        }
    }
}
