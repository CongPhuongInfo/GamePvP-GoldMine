Option Strict On
Option Explicit On

Imports System.Windows.Forms

Module ProgramMine

    <STAThread()>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MineForm())
    End Sub

End Module
