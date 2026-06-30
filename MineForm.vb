Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.IO
Imports System.Collections.Generic

Public Class MineForm
    Inherits Form

    Private Const SIDE_W As Integer = 220
    Private Const TICK_MS As Integer = 33   ' ~30fps, khop voi GoldMineGame.TICK_FPS
    Private Const DEFAULT_PORT As Integer = 9989

    Private game As GoldMineGame
    Private gameTimer As System.Windows.Forms.Timer
    Private playerCount As Integer = 1
    Private startLevel As Integer = 1

    ' === Mang (PvP Online) ===
    Private peer As NetworkPeer
    Private isOnlineMode As Boolean = False
    Private isHost As Boolean = False
    Private localPlayer As Integer = 0   ' host = 0, client = 1

    ' === Panels ===
    Private pnlMode As Panel
    Private pnlLevelSelect As Panel
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private lblStatus As Label

    Private boardPanel As DoubleBufferedPanel
    Private lblTime As Label
    Private lblLevel As Label
    Private lblScore0 As Label
    Private lblScore1 As Label
    Private lblLog As Label
    Private btnRestart As Button

    ' === Sprite ===
    Private itemSprites As New Dictionary(Of GoldMineGame.ItemKind, Image)()
    Private clawSprite As Image
    Private dirtTile As Image
    Private skyTile As Image
    Private spritesLoaded As Boolean = False

    Public Sub New()
        LoadSprites()
        InitUI()
    End Sub

    ' ============================================================
    '  LOAD SPRITE (co fallback ve hinh khoi neu thieu file)
    ' ============================================================
    Private Sub LoadSprites()
        Try
            Dim dir As String = Path.Combine(Application.StartupPath, "Assets")
            itemSprites(GoldMineGame.ItemKind.GoldSmall) = LoadImg(dir, "gold_small.png")
            itemSprites(GoldMineGame.ItemKind.GoldMedium) = LoadImg(dir, "gold_medium.png")
            itemSprites(GoldMineGame.ItemKind.GoldLarge) = LoadImg(dir, "gold_large.png")
            itemSprites(GoldMineGame.ItemKind.Rock) = LoadImg(dir, "rock.png")
            itemSprites(GoldMineGame.ItemKind.DiamondSmall) = LoadImg(dir, "diamond_small.png")
            itemSprites(GoldMineGame.ItemKind.DiamondLarge) = LoadImg(dir, "diamond_large.png")
            itemSprites(GoldMineGame.ItemKind.TNT) = LoadImg(dir, "tnt.png")
            itemSprites(GoldMineGame.ItemKind.MoneyBag) = LoadImg(dir, "moneybag.png")
            itemSprites(GoldMineGame.ItemKind.ClockBonus) = LoadImg(dir, "clock.png")
            itemSprites(GoldMineGame.ItemKind.MagnetBonus) = LoadImg(dir, "magnet.png")
            itemSprites(GoldMineGame.ItemKind.StrengthBonus) = LoadImg(dir, "strength.png")
            itemSprites(GoldMineGame.ItemKind.Skull) = LoadImg(dir, "skull.png")
            clawSprite = LoadImg(dir, "claw.png")
            dirtTile = LoadImg(dir, "tile_dirt.png")
            skyTile = LoadImg(dir, "tile_sky.png")
            spritesLoaded = True
        Catch ex As Exception
            spritesLoaded = False
        End Try
    End Sub

    Private Function LoadImg(dir As String, name As String) As Image
        Dim p As String = Path.Combine(dir, name)
        If File.Exists(p) Then Return Image.FromFile(p)
        Return Nothing
    End Function

    Private Sub InitUI()
        Me.Text = "Dao Vang - 2CongLC"
        Me.ClientSize = New Size(GoldMineGame.MINE_WIDTH + SIDE_W, GoldMineGame.MINE_HEIGHT)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(20, 20, 25)
        Me.KeyPreview = True
        AddHandler Me.KeyDown, AddressOf MineForm_KeyDown

        gameTimer = New System.Windows.Forms.Timer()
        gameTimer.Interval = TICK_MS
        AddHandler gameTimer.Tick, AddressOf GameTimer_Tick

        BuildModePanel()
        BuildLevelSelectPanel()
        BuildConnectPanel()
        BuildBoardPanel()
        BuildSidePanel()

        pnlLevelSelect.Visible = False
        pnlConnect.Visible = False
        SetGameControlsVisible(False)
    End Sub

    Private Sub SetGameControlsVisible(v As Boolean)
        boardPanel.Visible = v
        lblTime.Visible = v
        lblLevel.Visible = v
        lblScore0.Visible = v
        lblScore1.Visible = v AndAlso playerCount = 2
        lblLog.Visible = v
        btnRestart.Visible = v
    End Sub

    ' ============================================================
    '  MAN HINH CHON CHE DO
    ' ============================================================
    Private Sub BuildModePanel()
        pnlMode = New Panel()
        pnlMode.Dock = DockStyle.Fill
        pnlMode.BackColor = Color.FromArgb(20, 20, 25)

        Dim lbl As New Label()
        lbl.Text = "DAO VANG ONLINE"
        lbl.Font = New Font("Segoe UI", 24.0!, FontStyle.Bold)
        lbl.ForeColor = Color.Gold
        lbl.AutoSize = True
        pnlMode.Controls.Add(lbl)
        lbl.Location = New Point((Me.ClientSize.Width - 330) \ 2, 55)

        Dim btn1P As New Button()
        btn1P.Text = "⛏  1 Nguoi Choi (Offline, 3 Man)"
        btn1P.Font = New Font("Segoe UI", 11.5!, FontStyle.Bold)
        btn1P.Size = New Size(310, 50)
        btn1P.Location = New Point((Me.ClientSize.Width - 310) \ 2, 140)
        btn1P.BackColor = Color.DarkGoldenrod : btn1P.ForeColor = Color.White
        btn1P.FlatStyle = FlatStyle.Flat
        AddHandler btn1P.Click, Sub(s As Object, e As EventArgs)
                                     pnlMode.Visible = False
                                     pnlLevelSelect.Visible = True
                                 End Sub
        pnlMode.Controls.Add(btn1P)

        Dim btn2P As New Button()
        btn2P.Text = "👥  2 Nguoi Choi (Cung May)"
        btn2P.Font = New Font("Segoe UI", 11.5!, FontStyle.Bold)
        btn2P.Size = New Size(310, 50)
        btn2P.Location = New Point((Me.ClientSize.Width - 310) \ 2, 205)
        btn2P.BackColor = Color.SteelBlue : btn2P.ForeColor = Color.White
        btn2P.FlatStyle = FlatStyle.Flat
        AddHandler btn2P.Click, Sub(s As Object, e As EventArgs)
                                     isOnlineMode = False
                                     StartGame(2, 1)
                                 End Sub
        pnlMode.Controls.Add(btn2P)

        Dim btnOnline As New Button()
        btnOnline.Text = "🌐  PvP Online (LAN)"
        btnOnline.Font = New Font("Segoe UI", 11.5!, FontStyle.Bold)
        btnOnline.Size = New Size(310, 50)
        btnOnline.Location = New Point((Me.ClientSize.Width - 310) \ 2, 270)
        btnOnline.BackColor = Color.FromArgb(160, 50, 200) : btnOnline.ForeColor = Color.White
        btnOnline.FlatStyle = FlatStyle.Flat
        AddHandler btnOnline.Click, Sub(s As Object, e As EventArgs)
                                         pnlMode.Visible = False
                                         pnlConnect.Visible = True
                                     End Sub
        pnlMode.Controls.Add(btnOnline)

        Dim lblHelp As New Label()
        lblHelp.Text = "P1: SPACE tha moc   |   P2: ENTER tha moc"
        lblHelp.Font = New Font("Segoe UI", 9.5!)
        lblHelp.ForeColor = Color.LightGray
        lblHelp.AutoSize = True
        pnlMode.Controls.Add(lblHelp)
        lblHelp.Location = New Point((Me.ClientSize.Width - 280) \ 2, 335)

        Me.Controls.Add(pnlMode)
    End Sub

    ' ============================================================
    '  CHON MAN (chi cho che do 1 nguoi)
    ' ============================================================
    Private Sub BuildLevelSelectPanel()
        pnlLevelSelect = New Panel()
        pnlLevelSelect.Dock = DockStyle.Fill
        pnlLevelSelect.BackColor = Color.FromArgb(20, 20, 25)

        Dim lbl As New Label()
        lbl.Text = "CHON MAN BAT DAU"
        lbl.Font = New Font("Segoe UI", 18.0!, FontStyle.Bold)
        lbl.ForeColor = Color.Gold
        lbl.AutoSize = True
        pnlLevelSelect.Controls.Add(lbl)
        lbl.Location = New Point((Me.ClientSize.Width - 260) \ 2, 90)

        Dim levelInfo As String() = {
            "Man 1 - De: 90s, muc tieu 300 diem",
            "Man 2 - Vua: 80s, muc tieu 500 diem",
            "Man 3 - Kho: 70s, muc tieu 800 diem"
        }
        Dim i As Integer
        For i = 1 To 3
            Dim lv As Integer = i
            Dim btn As New Button()
            btn.Text = levelInfo(i - 1)
            btn.Font = New Font("Segoe UI", 10.5!, FontStyle.Bold)
            btn.Size = New Size(330, 48)
            btn.Location = New Point((Me.ClientSize.Width - 330) \ 2, 150 + (i - 1) * 60)
            btn.BackColor = Color.FromArgb(50 + i * 30, 90, 40)
            btn.ForeColor = Color.White
            btn.FlatStyle = FlatStyle.Flat
            AddHandler btn.Click, Sub(s As Object, e As EventArgs)
                                       isOnlineMode = False
                                       StartGame(1, lv)
                                   End Sub
            pnlLevelSelect.Controls.Add(btn)
        Next i

        Dim btnBack As New Button()
        btnBack.Text = "< Quay lai"
        btnBack.Size = New Size(100, 32)
        btnBack.Location = New Point(10, 10)
        btnBack.BackColor = Color.Gray : btnBack.ForeColor = Color.White
        btnBack.FlatStyle = FlatStyle.Flat
        AddHandler btnBack.Click, Sub(s As Object, e As EventArgs)
                                       pnlLevelSelect.Visible = False
                                       pnlMode.Visible = True
                                   End Sub
        pnlLevelSelect.Controls.Add(btnBack)

        Me.Controls.Add(pnlLevelSelect)
    End Sub

    ' ============================================================
    '  PANEL KET NOI LAN (PvP Online)
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(20, 20, 25)

        Dim lbl As New Label()
        lbl.Text = "PvP Online - Ket Noi LAN"
        lbl.Font = New Font("Segoe UI", 18.0!, FontStyle.Bold)
        lbl.ForeColor = Color.MediumPurple
        lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)
        lbl.Location = New Point((Me.ClientSize.Width - 310) \ 2, 60)

        Dim btnBack As New Button()
        btnBack.Text = "< Quay lai"
        btnBack.Size = New Size(100, 30)
        btnBack.Location = New Point(10, 10)
        btnBack.BackColor = Color.Gray : btnBack.ForeColor = Color.White
        btnBack.FlatStyle = FlatStyle.Flat
        AddHandler btnBack.Click, Sub(s As Object, e As EventArgs)
                                       pnlConnect.Visible = False
                                       pnlMode.Visible = True
                                   End Sub
        pnlConnect.Controls.Add(btnBack)

        Dim lblPort As New Label()
        lblPort.Text = "Port:"
        lblPort.ForeColor = Color.LightGray
        lblPort.AutoSize = True
        lblPort.Location = New Point((Me.ClientSize.Width - 220) \ 2, 130)
        pnlConnect.Controls.Add(lblPort)

        txtPort = New TextBox()
        txtPort.Text = DEFAULT_PORT.ToString()
        txtPort.Size = New Size(120, 26)
        txtPort.Location = New Point((Me.ClientSize.Width - 220) \ 2 + 50, 126)
        pnlConnect.Controls.Add(txtPort)

        Dim lblIPL As New Label()
        lblIPL.Text = "IP Host:"
        lblIPL.ForeColor = Color.LightGray
        lblIPL.AutoSize = True
        lblIPL.Location = New Point((Me.ClientSize.Width - 220) \ 2, 170)
        pnlConnect.Controls.Add(lblIPL)

        txtIP = New TextBox()
        txtIP.Text = "127.0.0.1"
        txtIP.Size = New Size(120, 26)
        txtIP.Location = New Point((Me.ClientSize.Width - 220) \ 2 + 70, 166)
        pnlConnect.Controls.Add(txtIP)

        Dim btnHost As New Button()
        btnHost.Text = "Tao Phong (Host)"
        btnHost.Size = New Size(220, 42)
        btnHost.Location = New Point((Me.ClientSize.Width - 220) \ 2, 220)
        btnHost.BackColor = Color.SeaGreen : btnHost.ForeColor = Color.White
        btnHost.FlatStyle = FlatStyle.Flat
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim btnJoin As New Button()
        btnJoin.Text = "Vao Phong (Join)"
        btnJoin.Size = New Size(220, 42)
        btnJoin.Location = New Point((Me.ClientSize.Width - 220) \ 2, 275)
        btnJoin.BackColor = Color.SteelBlue : btnJoin.ForeColor = Color.White
        btnJoin.FlatStyle = FlatStyle.Flat
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label()
        lblStatus.ForeColor = Color.Yellow
        lblStatus.AutoSize = True
        lblStatus.Location = New Point((Me.ClientSize.Width - 300) \ 2, 330)
        pnlConnect.Controls.Add(lblStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        isOnlineMode = True : isHost = True : localPlayer = 0
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        Try
            peer.StartHost(port)
            lblStatus.Text = "Dang cho doi thu tren port " & port.ToString() & "..."
        Catch ex As Exception
            MessageBox.Show("Loi: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim() = "" Then MessageBox.Show("Nhap IP.") : Return
        isOnlineMode = True : isHost = False : localPlayer = 1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        lblStatus.Text = "Dang ket noi..."
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        If Not isHost Then peer.SendLine("HELLO:Client")
    End Sub

    Private Sub Peer_Disconnected()
        gameTimer.Stop()
        Me.BeginInvoke(New Action(Sub()
            If game IsNot Nothing AndAlso game.GameOver Then Return
            MessageBox.Show("Mat ket noi.")
            SetGameControlsVisible(False)
            pnlConnect.Visible = False
            pnlLevelSelect.Visible = False
            pnlMode.Visible = True
        End Sub))
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("HELLO") Then
            If isHost Then
                playerCount = 2
                game = New GoldMineGame()
                game.ResetGame(2, 1)
                ShowGamePanel()
                BroadcastState()
                gameTimer.Start()
            End If

        ElseIf line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New GoldMineGame()
            game.Deserialize(line.Substring(6))
            playerCount = game.PlayerCount
            If Not boardPanel.Visible Then ShowGamePanel()
            boardPanel.Invalidate()
            RefreshSide()
            If game.GameOver Then
                gameTimer.Stop()
                Me.BeginInvoke(New Action(Sub()
                    MessageBox.Show(game.LastLog, "Ket thuc!")
                End Sub))
            End If

        ElseIf line.StartsWith("FIRE:") Then
            If isHost Then
                Dim pIdx As Integer
                Integer.TryParse(line.Substring(5), pIdx)
                game.FireHook(pIdx)
            End If
        End If
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    ' ============================================================
    '  BANG CHOI (VE GDI+ / SPRITE)
    ' ============================================================
    Private Sub BuildBoardPanel()
        boardPanel = New DoubleBufferedPanel()
        boardPanel.Location = New Point(0, 0)
        boardPanel.Size = New Size(GoldMineGame.MINE_WIDTH, GoldMineGame.MINE_HEIGHT)
        boardPanel.BackColor = Color.FromArgb(60, 40, 20)
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        AddHandler boardPanel.MouseDown, AddressOf BoardPanel_MouseDown
        Me.Controls.Add(boardPanel)
    End Sub

    Private Sub BuildSidePanel()
        lblLevel = New Label()
        lblLevel.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        lblLevel.ForeColor = Color.LightGreen
        lblLevel.AutoSize = True
        lblLevel.Location = New Point(GoldMineGame.MINE_WIDTH + 30, 15)
        Me.Controls.Add(lblLevel)

        lblTime = New Label()
        lblTime.Font = New Font("Segoe UI", 18.0!, FontStyle.Bold)
        lblTime.ForeColor = Color.White
        lblTime.AutoSize = True
        lblTime.Location = New Point(GoldMineGame.MINE_WIDTH + 30, 45)
        Me.Controls.Add(lblTime)

        lblScore0 = New Label()
        lblScore0.Font = New Font("Segoe UI", 13.0!, FontStyle.Bold)
        lblScore0.ForeColor = Color.Gold
        lblScore0.AutoSize = True
        lblScore0.Location = New Point(GoldMineGame.MINE_WIDTH + 30, 95)
        Me.Controls.Add(lblScore0)

        lblScore1 = New Label()
        lblScore1.Font = New Font("Segoe UI", 13.0!, FontStyle.Bold)
        lblScore1.ForeColor = Color.LightSkyBlue
        lblScore1.AutoSize = True
        lblScore1.Location = New Point(GoldMineGame.MINE_WIDTH + 30, 125)
        Me.Controls.Add(lblScore1)

        lblLog = New Label()
        lblLog.Font = New Font("Segoe UI", 9.5!)
        lblLog.ForeColor = Color.LightGray
        lblLog.Size = New Size(SIDE_W - 40, 220)
        lblLog.Location = New Point(GoldMineGame.MINE_WIDTH + 30, 170)
        Me.Controls.Add(lblLog)

        btnRestart = New Button()
        btnRestart.Text = "Choi Lai"
        btnRestart.Size = New Size(160, 40)
        btnRestart.Location = New Point(GoldMineGame.MINE_WIDTH + 30, GoldMineGame.MINE_HEIGHT - 60)
        btnRestart.BackColor = Color.DarkGoldenrod : btnRestart.ForeColor = Color.White
        btnRestart.FlatStyle = FlatStyle.Flat
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        Me.Controls.Add(btnRestart)
    End Sub

    ' ============================================================
    '  BAT DAU / CHOI LAI
    ' ============================================================
    Private Sub StartGame(pCount As Integer, lv As Integer)
        playerCount = pCount
        startLevel = lv
        game = New GoldMineGame()
        game.ResetGame(pCount, lv)
        ShowGamePanel()
        gameTimer.Start()
        boardPanel.Focus()
    End Sub

    Private Sub ShowGamePanel()
        pnlMode.Visible = False
        pnlLevelSelect.Visible = False
        pnlConnect.Visible = False
        SetGameControlsVisible(True)
        btnRestart.Visible = Not isOnlineMode OrElse isHost   ' client khong duoc restart
        RefreshSide()
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If game Is Nothing Then Return
        If isOnlineMode AndAlso Not isHost Then Return
        gameTimer.Stop()
        game.ResetGame(playerCount, startLevel)
        RefreshSide()
        gameTimer.Start()
        boardPanel.Focus()
        If isOnlineMode Then BroadcastState()
    End Sub

    ' ============================================================
    '  VONG LAP GAME (chi host hoac che do offline moi Tick)
    ' ============================================================
    Private Sub GameTimer_Tick(sender As Object, e As EventArgs)
        If game Is Nothing Then Return
        If isOnlineMode AndAlso Not isHost Then Return   ' client chi nhan STATE, khong tu tick

        game.Tick()
        boardPanel.Invalidate()
        RefreshSide()

        If isOnlineMode Then BroadcastState()

        If game.GameOver Then
            gameTimer.Stop()
            Me.BeginInvoke(New Action(Sub()
                MessageBox.Show(game.LastLog, "Ket thuc!")
            End Sub))
        End If
    End Sub

    Private Sub RefreshSide()
        If game Is Nothing Then Return
        Dim secLeft As Integer = Math.Max(0, game.TimeLeftFrames \ GoldMineGame.TICK_FPS)
        lblTime.Text = "⏱ " & secLeft.ToString() & "s"
        lblTime.ForeColor = If(secLeft <= 10, Color.OrangeRed, Color.White)
        lblLevel.Text = "Man " & game.Level.ToString() & "/" & GoldMineGame.MAX_LEVEL.ToString() & "   Muc tieu: " & game.TargetScore.ToString()

        lblScore0.Text = "Player 1: " & game.PlayerScore(0).ToString() & " diem"
        If playerCount = 2 Then
            lblScore1.Text = "Player 2: " & game.PlayerScore(1).ToString() & " diem"
            lblScore1.Visible = True
        Else
            lblScore1.Visible = False
        End If
        lblLog.Text = game.LastLog
    End Sub

    ' ============================================================
    '  INPUT
    ' ============================================================
    Private Sub MineForm_KeyDown(sender As Object, e As KeyEventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        If e.KeyCode = Keys.Space Then
            DoFire(0)
        ElseIf e.KeyCode = Keys.Enter AndAlso playerCount = 2 Then
            DoFire(1)
        End If
    End Sub

    Private Sub BoardPanel_MouseDown(sender As Object, e As MouseEventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        DoFire(0)
    End Sub

    ' Goi tha moc: offline xu ly truc tiep, online gui lenh ve host
    Private Sub DoFire(player As Integer)
        If isOnlineMode Then
            If player <> localPlayer Then Return   ' chi dieu khien duoc moc cua chinh minh
            If isHost Then
                game.FireHook(player)
            Else
                peer.SendLine("FIRE:" & player.ToString())
            End If
        Else
            game.FireHook(player)
        End If
    End Sub

    ' ============================================================
    '  VE BANG CHOI
    ' ============================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        If game Is Nothing Then Return
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        DrawBackground(g)

        Dim i As Integer
        For i = 0 To game.Items.Count - 1
            DrawItem(g, game.Items(i))
        Next i

        For i = 0 To game.Explosions.Count - 1
            Dim ex As GoldMineGame.ExplosionFx = game.Explosions(i)
            Dim alpha As Integer = CInt(Math.Min(255, ex.Timer * 17))
            Dim r As Single = (15 - ex.Timer) * 6.0F + 10.0F
            Using fxBrush As New SolidBrush(Color.FromArgb(alpha, 255, 140, 0))
                g.FillEllipse(fxBrush, ex.X - r, ex.Y - r, r * 2, r * 2)
            End Using
        Next i

        Dim colors() As Color = {Color.LimeGreen, Color.DeepSkyBlue}
        Dim p As Integer
        For p = 0 To GoldMineGame.MAX_PLAYERS - 1
            If Not game.PlayerActive(p) Then Continue For
            DrawHook(g, p, colors(p))
        Next p

        Using infoBrush As New SolidBrush(Color.White)
            g.DrawString("Man " & game.Level.ToString() & " - Muc tieu " & game.TargetScore.ToString() & " diem", New Font("Segoe UI", 9.0!), infoBrush, 10, 8)
        End Using
    End Sub

    Private Sub DrawBackground(g As Graphics)
        If spritesLoaded AndAlso dirtTile IsNot Nothing AndAlso skyTile IsNot Nothing Then
            Dim tileSize As Integer = 128
            Dim x As Integer, y As Integer
            For y = 0 To GoldMineGame.SURFACE_Y Step tileSize
                For x = 0 To GoldMineGame.MINE_WIDTH Step tileSize
                    g.DrawImage(skyTile, x, y, tileSize, tileSize)
                Next x
            Next y
            For y = GoldMineGame.SURFACE_Y To GoldMineGame.MINE_HEIGHT Step tileSize
                For x = 0 To GoldMineGame.MINE_WIDTH Step tileSize
                    g.DrawImage(dirtTile, x, y, tileSize, tileSize)
                Next x
            Next y
        Else
            Using dirtBrush As New SolidBrush(Color.FromArgb(70, 45, 25))
                g.FillRectangle(dirtBrush, 0, GoldMineGame.SURFACE_Y, GoldMineGame.MINE_WIDTH, GoldMineGame.MINE_HEIGHT - GoldMineGame.SURFACE_Y)
            End Using
            Using skyBrush As New SolidBrush(Color.FromArgb(135, 195, 235))
                g.FillRectangle(skyBrush, 0, 0, GoldMineGame.MINE_WIDTH, GoldMineGame.SURFACE_Y)
            End Using
        End If
        Using surfacePen As New Pen(Color.SaddleBrown, 3.0!)
            g.DrawLine(surfacePen, 0, GoldMineGame.SURFACE_Y, GoldMineGame.MINE_WIDTH, GoldMineGame.SURFACE_Y)
        End Using
    End Sub

    Private Sub DrawHook(g As Graphics, p As Integer, col As Color)
        Dim baseX As Single = game.HookBaseX(p)
        Dim baseY As Single = game.HookBaseY(p)
        Dim tipX As Single = game.GetHookTipX(p)
        Dim tipY As Single = game.GetHookTipY(p)

        Using pivotBrush As New SolidBrush(col)
            g.FillEllipse(pivotBrush, baseX - 8, baseY - 8, 16, 16)
        End Using

        Using ropePen As New Pen(col, 2.5!)
            g.DrawLine(ropePen, baseX, baseY, tipX, tipY)
        End Using

        If spritesLoaded AndAlso clawSprite IsNot Nothing Then
            Dim angleDeg As Single = game.HookAngle(p) * 180.0F / CSng(Math.PI)
            Dim state As GraphicsState = g.Save()
            g.TranslateTransform(tipX, tipY - 14)
            g.RotateTransform(angleDeg)
            g.DrawImage(clawSprite, -16, -16, 32, 32)
            g.Restore(state)
        Else
            Using clawBrush As New SolidBrush(Color.Silver)
                g.FillEllipse(clawBrush, tipX - 7, tipY - 7, 14, 14)
            End Using
        End If
    End Sub

    Private Sub DrawItem(g As Graphics, it As GoldMineGame.MineItem)
        Dim r As Single = it.Radius
        Dim x As Single = it.X - r
        Dim y As Single = it.Y - r
        Dim d As Single = r * 2

        Dim spr As Image = Nothing
        If spritesLoaded Then itemSprites.TryGetValue(it.Kind, spr)

        If spr IsNot Nothing Then
            g.DrawImage(spr, x, y, d, d)
            Return
        End If

        ' --- Fallback: ve hinh khoi neu chua co sprite ---
        Select Case it.Kind
            Case GoldMineGame.ItemKind.GoldSmall, GoldMineGame.ItemKind.GoldMedium, GoldMineGame.ItemKind.GoldLarge
                Using b As New SolidBrush(Color.Goldenrod) : g.FillEllipse(b, x, y, d, d) : End Using
            Case GoldMineGame.ItemKind.Rock
                Using b As New SolidBrush(Color.Gray) : g.FillEllipse(b, x, y, d, d) : End Using
            Case GoldMineGame.ItemKind.DiamondSmall, GoldMineGame.ItemKind.DiamondLarge
                Using b As New SolidBrush(Color.Aqua) : g.FillEllipse(b, x, y, d, d) : End Using
            Case GoldMineGame.ItemKind.TNT
                Using b As New SolidBrush(Color.Firebrick) : g.FillRectangle(b, x, y, d, d) : End Using
            Case Else
                Using b As New SolidBrush(Color.White) : g.FillEllipse(b, x, y, d, d) : End Using
        End Select
    End Sub

End Class

' Panel co double buffering de chong nhay man hinh
Public Class DoubleBufferedPanel
    Inherits Panel
    Public Sub New()
        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.UserPaint, True)
        Me.UpdateStyles()
    End Sub
End Class
