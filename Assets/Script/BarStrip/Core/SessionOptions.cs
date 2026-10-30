using System;

// Pengaturan perilaku sesi Media 4/6 (diatur dari Inspector Activity4Controller)
[Serializable]
public class SessionOptions
{
    public bool MoveFromKotak = true;     // true: balok dipindahkan dari Kotak ke area (bisa dikembalikan); false: disalin
    public bool CheckWholes = false;      // true: bilangan bulat di area atas juga dicek, selain sisa
}