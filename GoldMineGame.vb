Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

Public Class GoldMineGame

    Public Const MINE_WIDTH As Integer = 760
    Public Const MINE_HEIGHT As Integer = 520
    Public Const SURFACE_Y As Integer = 40
    Public Const ROUND_SECONDS As Integer = 90
    Public Const MAX_PLAYERS As Integer = 2
    Public Const TICK_FPS As Integer = 30   ' so tick/giay gia dinh (dung de tinh TimeLeftFrames)
    Public Const MAX_LEVEL As Integer = 3

    Public Enum ItemKind As Byte
        GoldSmall = 0
        GoldMedium = 1
        GoldLarge = 2
        Rock = 3
        DiamondSmall = 4
        DiamondLarge = 5
        TNT = 6
        MoneyBag = 7
        Skull = 8
        ClockBonus = 9
        MagnetBonus = 10
        StrengthBonus = 11
    End Enum

    Public Enum HookState As Byte
        Swinging = 0
        Extending = 1
        Retracting = 2
    End Enum

    Public Structure MineItem
        Public X As Single
        Public Y As Single
        Public Radius As Single
        Public Weight As Single      ' cang nang keo ve cang cham
        Public Value As Integer      ' diem khi mang ve dich
        Public Kind As ItemKind
        Public Active As Boolean
        Public Vx As Single          ' van toc khi bay (sau khi TNT no)
        Public Vy As Single
        Public Flying As Boolean
    End Structure

    Public Structure ExplosionFx
        Public X As Single
        Public Y As Single
        Public Timer As Integer
    End Structure

    ' === Trang thai moc cau, theo tung player (mang san index de mo rong PvP) ===
    Public HookBaseX(MAX_PLAYERS - 1) As Single
    Public HookBaseY(MAX_PLAYERS - 1) As Single
    Public HookAngle(MAX_PLAYERS - 1) As Single
    Public HookAngleDir(MAX_PLAYERS - 1) As Integer
    Public HookLength(MAX_PLAYERS - 1) As Single
    Public HookState_(MAX_PLAYERS - 1) As HookState
    Public HookedItemIndex(MAX_PLAYERS - 1) As Integer
    Public PlayerScore(MAX_PLAYERS - 1) As Integer
    Public PlayerStrengthTimer(MAX_PLAYERS - 1) As Integer
    Public PlayerActive(MAX_PLAYERS - 1) As Boolean

    Public PlayerCount As Integer = 1

    Public Const SWING_SPEED As Single = 0.028!
    Public Const MIN_ANGLE As Single = -1.45!
    Public Const MAX_ANGLE As Single = 1.45!
    Public Const EXTEND_SPEED As Single = 9.0!
    Public Const RETRACT_BASE_SPEED As Single = 7.0!
    Public Const GRAVITY As Single = 0.5!
    Public Const EXPLOSION_RADIUS As Single = 90.0!

    Public Items As New List(Of MineItem)()
    Public Explosions As New List(Of ExplosionFx)()
    Private rng As New Random()

    Public TimeLeftFrames As Integer
    Public GameOver As Boolean
    Public LastLog As String
    Public TargetScore As Integer = 300
    Public Level As Integer = 1
    Public LevelCleared As Boolean = False   ' bao UI hien thong bao "len man" 1 lan

    Public Sub New()
        ResetGame(1)
    End Sub

    Public Sub ResetGame(playerCount As Integer)
        ResetGame(playerCount, 1)
    End Sub

    Public Sub ResetGame(playerCount As Integer, startLevel As Integer)
        PlayerCount = Math.Max(1, Math.Min(MAX_PLAYERS, playerCount))
        Level = Math.Max(1, Math.Min(MAX_LEVEL, startLevel))
        Items.Clear()
        Explosions.Clear()
        GameOver = False
        LevelCleared = False
        LastLog = "Man " & Level.ToString() & " - Bam SPACE / Click de tha moc cau!"
        TargetScore = GetTargetScoreForLevel(Level)
        TimeLeftFrames = GetRoundSecondsForLevel(Level) * TICK_FPS

        Dim i As Integer
        For i = 0 To MAX_PLAYERS - 1
            PlayerActive(i) = (i < PlayerCount)
            PlayerScore(i) = 0
            PlayerStrengthTimer(i) = 0
            HookState_(i) = HookState.Swinging
            HookAngle(i) = 0
            HookAngleDir(i) = 1
            HookLength(i) = 0
            HookedItemIndex(i) = -1
            If PlayerCount = 1 Then
                HookBaseX(i) = MINE_WIDTH / 2.0!
            Else
                HookBaseX(i) = MINE_WIDTH * (0.3F + 0.4F * i)
            End If
            HookBaseY(i) = SURFACE_Y
        Next i

        SpawnInitialItems()
    End Sub

    ' ===== Cau hinh do kho theo tung man =====
    Private Function GetTargetScoreForLevel(lv As Integer) As Integer
        Select Case lv
            Case 1 : Return 300
            Case 2 : Return 500
            Case Else : Return 800
        End Select
    End Function

    Private Function GetRoundSecondsForLevel(lv As Integer) As Integer
        Select Case lv
            Case 1 : Return 90
            Case 2 : Return 80
            Case Else : Return 70
        End Select
    End Function

    Private Function GetSwingSpeedForLevel() As Single
        Select Case Level
            Case 1 : Return SWING_SPEED
            Case 2 : Return SWING_SPEED * 1.35!
            Case Else : Return SWING_SPEED * 1.7!
        End Select
    End Function

    Private Sub SpawnInitialItems()
        Dim count As Integer = 16
        Dim i As Integer
        For i = 1 To count
            Items.Add(MakeRandomItem())
        Next i
    End Sub

    Private Function MakeRandomItem() As MineItem
        Dim it As New MineItem()
        Dim roll As Integer = rng.Next(100)
        ' Cang len man cao, vang nho/da/tnt xuat hien nhieu hon, vang to/kim cuong hiem hon
        Dim rockBonus As Integer = (Level - 1) * 6      ' +6%/+12% o man 2/3
        Dim tntBonus As Integer = (Level - 1) * 3
        If roll < 28 - (Level - 1) * 3 Then
            it.Kind = ItemKind.GoldSmall : it.Radius = 14 : it.Weight = 1.0! : it.Value = 50
        ElseIf roll < 48 - (Level - 1) * 4 Then
            it.Kind = ItemKind.GoldMedium : it.Radius = 20 : it.Weight = 2.0! : it.Value = 120
        ElseIf roll < 58 - (Level - 1) * 4 Then
            it.Kind = ItemKind.GoldLarge : it.Radius = 30 : it.Weight = 4.0! : it.Value = 300
        ElseIf roll < 75 + rockBonus Then
            it.Kind = ItemKind.Rock : it.Radius = 22 : it.Weight = 3.0! + (Level - 1) * 0.6! : it.Value = 10
        ElseIf roll < 85 + rockBonus Then
            it.Kind = ItemKind.DiamondSmall : it.Radius = 12 : it.Weight = 0.6! : it.Value = 200
        ElseIf roll < 90 + rockBonus Then
            it.Kind = ItemKind.DiamondLarge : it.Radius = 18 : it.Weight = 1.2! : it.Value = 500
        ElseIf roll < 94 + rockBonus + tntBonus Then
            it.Kind = ItemKind.TNT : it.Radius = 16 : it.Weight = 1.0! : it.Value = 30
        ElseIf roll < 97 + rockBonus + tntBonus Then
            it.Kind = ItemKind.MoneyBag : it.Radius = 16 : it.Weight = 1.5! : it.Value = 150
        ElseIf roll < 98 + rockBonus + tntBonus Then
            it.Kind = ItemKind.ClockBonus : it.Radius = 14 : it.Weight = 0.5! : it.Value = 0
        ElseIf roll < 99 + rockBonus + tntBonus Then
            it.Kind = ItemKind.MagnetBonus : it.Radius = 14 : it.Weight = 0.5! : it.Value = 0
        Else
            it.Kind = ItemKind.StrengthBonus : it.Radius = 14 : it.Weight = 0.5! : it.Value = 0
        End If

        Dim tries As Integer = 0
        Do
            it.X = CSng(rng.Next(CInt(it.Radius) + 10, MINE_WIDTH - CInt(it.Radius) - 10))
            it.Y = CSng(rng.Next(SURFACE_Y + 60, MINE_HEIGHT - CInt(it.Radius) - 10))
            tries += 1
        Loop While tries < 20 AndAlso IsOverlapping(it)
        it.Active = True
        it.Flying = False
        Return it
    End Function

    Private Function IsOverlapping(candidate As MineItem) As Boolean
        Dim i As Integer
        For i = 0 To Items.Count - 1
            If Not Items(i).Active Then Continue For
            Dim dx As Single = Items(i).X - candidate.X
            Dim dy As Single = Items(i).Y - candidate.Y
            Dim distSq As Single = dx * dx + dy * dy
            Dim minDist As Single = Items(i).Radius + candidate.Radius + 4.0!
            If distSq < minDist * minDist Then Return True
        Next i
        Return False
    End Function

    ' Goi khi nguoi choi bam SPACE / click chuot de tha moc
    Public Function FireHook(player As Integer) As Boolean
        If GameOver Then Return False
        If player < 0 OrElse player >= MAX_PLAYERS OrElse Not PlayerActive(player) Then Return False
        If HookState_(player) <> HookState.Swinging Then Return False
        HookState_(player) = HookState.Extending
        Return True
    End Function

    Public Sub Tick()
        If GameOver Then Return

        TimeLeftFrames -= 1
        If TimeLeftFrames <= 0 Then
            TimeLeftFrames = 0
            GameOver = True
            LastLog = BuildEndMessage()
            Return
        End If

        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            If Not PlayerActive(p) Then Continue For
            If PlayerStrengthTimer(p) > 0 Then PlayerStrengthTimer(p) -= 1
            UpdateHook(p)
        Next p

        UpdateFlyingItems()
        UpdateExplosions()
    End Sub

    Private Sub UpdateHook(p As Integer)
        Select Case HookState_(p)
            Case HookState.Swinging
                HookAngle(p) += GetSwingSpeedForLevel() * HookAngleDir(p)
                If HookAngle(p) >= MAX_ANGLE Then
                    HookAngle(p) = MAX_ANGLE : HookAngleDir(p) = -1
                ElseIf HookAngle(p) <= MIN_ANGLE Then
                    HookAngle(p) = MIN_ANGLE : HookAngleDir(p) = 1
                End If

            Case HookState.Extending
                HookLength(p) += EXTEND_SPEED
                Dim tipX As Single = HookBaseX(p) + HookLength(p) * CSng(Math.Sin(HookAngle(p)))
                Dim tipY As Single = HookBaseY(p) + HookLength(p) * CSng(Math.Cos(HookAngle(p)))

                If tipX < 0 OrElse tipX > MINE_WIDTH OrElse tipY > MINE_HEIGHT Then
                    HookState_(p) = HookState.Retracting
                    Return
                End If

                Dim hitIndex As Integer = FindItemAt(tipX, tipY)
                If hitIndex >= 0 Then
                    If Items(hitIndex).Kind = ItemKind.TNT Then
                        ExplodeItem(hitIndex)
                        HookedItemIndex(p) = -1
                        HookState_(p) = HookState.Retracting
                    Else
                        HookedItemIndex(p) = hitIndex
                        Dim it As MineItem = Items(hitIndex)
                        it.Active = False
                        it.Flying = False
                        Items(hitIndex) = it
                        HookState_(p) = HookState.Retracting
                    End If
                End If

            Case HookState.Retracting
                Dim speed As Single = RETRACT_BASE_SPEED
                If HookedItemIndex(p) >= 0 Then
                    Dim w As Single = Items(HookedItemIndex(p)).Weight
                    speed = RETRACT_BASE_SPEED / (0.6F + w * 0.5F)
                End If
                If PlayerStrengthTimer(p) > 0 Then speed *= 1.8!

                HookLength(p) -= speed
                If HookLength(p) < 0 Then HookLength(p) = 0

                Dim tipX2 As Single = HookBaseX(p) + HookLength(p) * CSng(Math.Sin(HookAngle(p)))
                Dim tipY2 As Single = HookBaseY(p) + HookLength(p) * CSng(Math.Cos(HookAngle(p)))
                If HookedItemIndex(p) >= 0 Then
                    Dim it As MineItem = Items(HookedItemIndex(p))
                    it.X = tipX2 : it.Y = tipY2
                    Items(HookedItemIndex(p)) = it
                End If

                If HookLength(p) <= 0 Then
                    If HookedItemIndex(p) >= 0 Then
                        CollectItem(p, HookedItemIndex(p))
                        HookedItemIndex(p) = -1
                    End If
                    HookState_(p) = HookState.Swinging
                End If
        End Select
    End Sub

    Private Function FindItemAt(x As Single, y As Single) As Integer
        Dim i As Integer
        Dim hookRadius As Single = 8.0!
        For i = 0 To Items.Count - 1
            If Not Items(i).Active Then Continue For
            If Items(i).Flying Then Continue For
            Dim dx As Single = Items(i).X - x
            Dim dy As Single = Items(i).Y - y
            Dim dist As Single = CSng(Math.Sqrt(dx * dx + dy * dy))
            If dist <= Items(i).Radius + hookRadius Then Return i
        Next i
        Return -1
    End Function

    Private Sub CollectItem(p As Integer, idx As Integer)
        Dim it As MineItem = Items(idx)
        Select Case it.Kind
            Case ItemKind.ClockBonus
                TimeLeftFrames += 10 * TICK_FPS
                LastLog = "Player " & (p + 1).ToString() & " nhat duoc Dong Ho +10s!"
            Case ItemKind.MagnetBonus
                PlayerStrengthTimer(p) = Math.Max(PlayerStrengthTimer(p), 5 * TICK_FPS)
                LastLog = "Player " & (p + 1).ToString() & " nhat duoc Nam Cham!"
            Case ItemKind.StrengthBonus
                PlayerStrengthTimer(p) = Math.Max(PlayerStrengthTimer(p), 8 * TICK_FPS)
                LastLog = "Player " & (p + 1).ToString() & " duoc tang Suc Manh!"
            Case ItemKind.Skull
                PlayerScore(p) = Math.Max(0, PlayerScore(p) - 50)
                LastLog = "Player " & (p + 1).ToString() & " trung Dau Lau, -50 diem!"
            Case Else
                PlayerScore(p) += it.Value
                LastLog = String.Format("Player {0} thu duoc {1}, +{2} diem!", p + 1, ItemName(it.Kind), it.Value)
        End Select

        Items.RemoveAt(idx)
        Dim other As Integer
        For other = 0 To MAX_PLAYERS - 1
            If HookedItemIndex(other) > idx Then HookedItemIndex(other) -= 1
        Next other

        Items.Add(MakeRandomItem())
        CheckWinCondition()
    End Sub

    Private Function ItemName(k As ItemKind) As String
        Select Case k
            Case ItemKind.GoldSmall : Return "vang nho"
            Case ItemKind.GoldMedium : Return "vang vua"
            Case ItemKind.GoldLarge : Return "vang to"
            Case ItemKind.Rock : Return "da"
            Case ItemKind.DiamondSmall : Return "kim cuong nho"
            Case ItemKind.DiamondLarge : Return "kim cuong to"
            Case ItemKind.MoneyBag : Return "tui tien"
            Case Else : Return "vat pham"
        End Select
    End Function

    Private Sub ExplodeItem(idx As Integer)
        Dim center As MineItem = Items(idx)
        Dim fx As New ExplosionFx()
        fx.X = center.X : fx.Y = center.Y : fx.Timer = 15
        Explosions.Add(fx)

        Dim i As Integer
        For i = 0 To Items.Count - 1
            If i = idx Then Continue For
            If Not Items(i).Active OrElse Items(i).Flying Then Continue For
            Dim dx As Single = Items(i).X - center.X
            Dim dy As Single = Items(i).Y - center.Y
            Dim dist As Single = CSng(Math.Sqrt(dx * dx + dy * dy))
            If dist < EXPLOSION_RADIUS Then
                Dim it As MineItem = Items(i)
                Dim angle As Single = CSng(Math.Atan2(dy, dx))
                If dist < 0.01! Then angle = CSng(rng.NextDouble() * Math.PI * 2.0)
                Dim force As Single = (1.0F - dist / EXPLOSION_RADIUS) * 14.0F
                it.Vx = CSng(Math.Cos(angle)) * force
                it.Vy = CSng(Math.Sin(angle)) * force - 4.0F
                it.Flying = True
                Items(i) = it
            End If
        Next i

        Items.RemoveAt(idx)
        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            If HookedItemIndex(p) > idx Then HookedItemIndex(p) -= 1
        Next p
        Items.Add(MakeRandomItem())
    End Sub

    Private Sub UpdateFlyingItems()
        Dim i As Integer
        For i = 0 To Items.Count - 1
            If Not Items(i).Flying Then Continue For
            Dim it As MineItem = Items(i)
            it.Vy += GRAVITY
            it.X += it.Vx
            it.Y += it.Vy

            If it.X < it.Radius Then it.X = it.Radius : it.Vx = -it.Vx * 0.4F
            If it.X > MINE_WIDTH - it.Radius Then it.X = MINE_WIDTH - it.Radius : it.Vx = -it.Vx * 0.4F
            If it.Y > MINE_HEIGHT - it.Radius Then
                it.Y = MINE_HEIGHT - it.Radius
                it.Vy = -it.Vy * 0.35F
                it.Vx *= 0.7F
                If Math.Abs(it.Vy) < 1.0F AndAlso Math.Abs(it.Vx) < 0.5F Then
                    it.Flying = False
                    it.Vx = 0 : it.Vy = 0
                End If
            End If
            Items(i) = it
        Next i
    End Sub

    Private Sub UpdateExplosions()
        Dim i As Integer
        For i = Explosions.Count - 1 To 0 Step -1
            Dim ex As ExplosionFx = Explosions(i)
            ex.Timer -= 1
            If ex.Timer <= 0 Then
                Explosions.RemoveAt(i)
            Else
                Explosions(i) = ex
            End If
        Next i
    End Sub

    Private Sub CheckWinCondition()
        If PlayerCount = 1 Then
            If PlayerScore(0) >= TargetScore Then
                If Level < MAX_LEVEL Then
                    AdvanceLevel()
                Else
                    GameOver = True
                    LastLog = "CHIEN THANG! Ban da pha dao voi " & PlayerScore(0).ToString() & " diem!"
                End If
            End If
        End If
    End Sub

    Private Sub AdvanceLevel()
        Dim keepScore As Integer = PlayerScore(0)
        Level += 1
        LevelCleared = True
        Items.Clear()
        Explosions.Clear()
        TargetScore = GetTargetScoreForLevel(Level)
        TimeLeftFrames = GetRoundSecondsForLevel(Level) * TICK_FPS
        PlayerScore(0) = keepScore
        HookState_(0) = HookState.Swinging
        HookAngle(0) = 0 : HookAngleDir(0) = 1 : HookLength(0) = 0
        HookedItemIndex(0) = -1
        SpawnInitialItems()
        LastLog = "LEN MAN " & Level.ToString() & "! Diem giu nguyen: " & keepScore.ToString()
    End Sub

    Private Function BuildEndMessage() As String
        If PlayerCount = 1 Then
            Return "Het gio! Diem cua ban: " & PlayerScore(0).ToString()
        Else
            If PlayerScore(0) > PlayerScore(1) Then
                Return "Het gio! Player 1 THANG voi " & PlayerScore(0).ToString() & " diem!"
            ElseIf PlayerScore(1) > PlayerScore(0) Then
                Return "Het gio! Player 2 THANG voi " & PlayerScore(1).ToString() & " diem!"
            Else
                Return "Het gio! HOA, ca 2 cung " & PlayerScore(0).ToString() & " diem!"
            End If
        End If
    End Function

    Public Function GetHookTipX(p As Integer) As Single
        Return HookBaseX(p) + HookLength(p) * CSng(Math.Sin(HookAngle(p)))
    End Function

    Public Function GetHookTipY(p As Integer) As Single
        Return HookBaseY(p) + HookLength(p) * CSng(Math.Cos(HookAngle(p)))
    End Function

    ' ============================================================
    '  SERIALIZE / DESERIALIZE cho PvP mang (host la nguon su that)
    ' ============================================================
    Public Function Serialize() As String
        Dim sb As New StringBuilder()
        Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture

        sb.Append(PlayerCount.ToString()) : sb.Append("|")
        sb.Append(Level.ToString()) : sb.Append("|")
        sb.Append(TargetScore.ToString()) : sb.Append("|")
        sb.Append(TimeLeftFrames.ToString()) : sb.Append("|")
        sb.Append(If(GameOver, "1", "0")) : sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " ")) : sb.Append("|")

        ' Hooks + diem theo tung player
        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            sb.Append(If(PlayerActive(p), "1", "0")) : sb.Append(",")
            sb.Append(HookBaseX(p).ToString(inv)) : sb.Append(",")
            sb.Append(HookBaseY(p).ToString(inv)) : sb.Append(",")
            sb.Append(HookAngle(p).ToString(inv)) : sb.Append(",")
            sb.Append(HookAngleDir(p).ToString()) : sb.Append(",")
            sb.Append(HookLength(p).ToString(inv)) : sb.Append(",")
            sb.Append(CInt(HookState_(p)).ToString()) : sb.Append(",")
            sb.Append(HookedItemIndex(p).ToString()) : sb.Append(",")
            sb.Append(PlayerScore(p).ToString()) : sb.Append(",")
            sb.Append(PlayerStrengthTimer(p).ToString())
            If p < MAX_PLAYERS - 1 Then sb.Append(";")
        Next p
        sb.Append("|")

        ' Items
        Dim i As Integer
        For i = 0 To Items.Count - 1
            Dim it As MineItem = Items(i)
            sb.Append(it.X.ToString(inv)) : sb.Append(",")
            sb.Append(it.Y.ToString(inv)) : sb.Append(",")
            sb.Append(it.Radius.ToString(inv)) : sb.Append(",")
            sb.Append(it.Weight.ToString(inv)) : sb.Append(",")
            sb.Append(it.Value.ToString()) : sb.Append(",")
            sb.Append(CInt(it.Kind).ToString()) : sb.Append(",")
            sb.Append(If(it.Active, "1", "0")) : sb.Append(",")
            sb.Append(it.Vx.ToString(inv)) : sb.Append(",")
            sb.Append(it.Vy.ToString(inv)) : sb.Append(",")
            sb.Append(If(it.Flying, "1", "0"))
            If i < Items.Count - 1 Then sb.Append(";")
        Next i
        sb.Append("|")

        ' Explosions
        For i = 0 To Explosions.Count - 1
            Dim ex As ExplosionFx = Explosions(i)
            sb.Append(ex.X.ToString(inv)) : sb.Append(",")
            sb.Append(ex.Y.ToString(inv)) : sb.Append(",")
            sb.Append(ex.Timer.ToString())
            If i < Explosions.Count - 1 Then sb.Append(";")
        Next i

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        If parts.Length < 9 Then Return
        Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture

        Integer.TryParse(parts(0), PlayerCount)
        Integer.TryParse(parts(1), Level)
        Integer.TryParse(parts(2), TargetScore)
        Integer.TryParse(parts(3), TimeLeftFrames)
        GameOver = (parts(4) = "1")
        LastLog = parts(5)

        Dim hookEntries As String() = parts(6).Split(";"c)
        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            If p >= hookEntries.Length Then Exit For
            Dim hp As String() = hookEntries(p).Split(","c)
            If hp.Length >= 10 Then
                PlayerActive(p) = (hp(0) = "1")
                Single.TryParse(hp(1), System.Globalization.NumberStyles.Float, inv, HookBaseX(p))
                Single.TryParse(hp(2), System.Globalization.NumberStyles.Float, inv, HookBaseY(p))
                Single.TryParse(hp(3), System.Globalization.NumberStyles.Float, inv, HookAngle(p))
                Integer.TryParse(hp(4), HookAngleDir(p))
                Single.TryParse(hp(5), System.Globalization.NumberStyles.Float, inv, HookLength(p))
                Dim hs As Integer = 0 : Integer.TryParse(hp(6), hs) : HookState_(p) = CType(hs, HookState)
                Integer.TryParse(hp(7), HookedItemIndex(p))
                Integer.TryParse(hp(8), PlayerScore(p))
                Integer.TryParse(hp(9), PlayerStrengthTimer(p))
            End If
        Next p

        Items.Clear()
        If parts.Length > 7 AndAlso parts(7).Length > 0 Then
            For Each entry As String In parts(7).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim ip As String() = entry.Split(","c)
                If ip.Length >= 10 Then
                    Dim it As New MineItem()
                    Single.TryParse(ip(0), System.Globalization.NumberStyles.Float, inv, it.X)
                    Single.TryParse(ip(1), System.Globalization.NumberStyles.Float, inv, it.Y)
                    Single.TryParse(ip(2), System.Globalization.NumberStyles.Float, inv, it.Radius)
                    Single.TryParse(ip(3), System.Globalization.NumberStyles.Float, inv, it.Weight)
                    Integer.TryParse(ip(4), it.Value)
                    Dim kv As Integer = 0 : Integer.TryParse(ip(5), kv) : it.Kind = CType(kv, ItemKind)
                    it.Active = (ip(6) = "1")
                    Single.TryParse(ip(7), System.Globalization.NumberStyles.Float, inv, it.Vx)
                    Single.TryParse(ip(8), System.Globalization.NumberStyles.Float, inv, it.Vy)
                    it.Flying = (ip(9) = "1")
                    Items.Add(it)
                End If
            Next
        End If

        Explosions.Clear()
        If parts.Length > 8 AndAlso parts(8).Length > 0 Then
            For Each entry As String In parts(8).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim ep As String() = entry.Split(","c)
                If ep.Length >= 3 Then
                    Dim ex As New ExplosionFx()
                    Single.TryParse(ep(0), System.Globalization.NumberStyles.Float, inv, ex.X)
                    Single.TryParse(ep(1), System.Globalization.NumberStyles.Float, inv, ex.Y)
                    Integer.TryParse(ep(2), ex.Timer)
                    Explosions.Add(ex)
                End If
            Next
        End If
    End Sub

End Class
