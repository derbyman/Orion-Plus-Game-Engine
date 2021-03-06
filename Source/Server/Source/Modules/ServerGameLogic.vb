﻿Imports Orion

Module ServerGameLogic
    Function GetTotalMapPlayers(ByVal MapNum As Integer) As Integer
        Dim i As Integer, n As Integer
        n = 0

        For i = 1 To GetPlayersOnline()
            If IsPlaying(i) And GetPlayerMap(i) = MapNum Then
                n = n + 1
            End If
        Next

        GetTotalMapPlayers = n
    End Function

    Public Function GetPlayersOnline() As Integer
        Dim x As Integer
        x = 0
        For i = 1 To MAX_PLAYERS
            If TempPlayer(i).InGame = True Then
                x = x + 1
            End If
        Next
        GetPlayersOnline = x
    End Function

    Function GetNpcMaxVital(ByVal NpcNum As Integer, ByVal Vital As Vitals) As Integer
        GetNpcMaxVital = 0

        ' Prevent subscript out of range
        If NpcNum <= 0 Or NpcNum > MAX_NPCS Then Exit Function

        Select Case Vital
            Case Vitals.HP
                GetNpcMaxVital = Npc(NpcNum).Hp
            Case Vitals.MP
                GetNpcMaxVital = Npc(NpcNum).Stat(Stats.Intelligence) * 2
            Case Vitals.SP
                GetNpcMaxVital = Npc(NpcNum).Stat(Stats.Spirit) * 2
        End Select

    End Function

    Function FindPlayer(ByVal Name As String) As Integer
        Dim i As Integer

        For i = 1 To GetPlayersOnline()
            If IsPlaying(i) Then
                ' Make sure we dont try to check a name thats to small
                If Len(GetPlayerName(i)) >= Len(Trim$(Name)) Then
                    If UCase$(Mid$(GetPlayerName(i), 1, Len(Trim$(Name)))) = UCase$(Trim$(Name)) Then
                        FindPlayer = i
                        Exit Function
                    End If
                End If
            End If
        Next

        FindPlayer = 0
    End Function

    Sub SpawnItem(ByVal itemNum As Integer, ByVal ItemVal As Integer, ByVal MapNum As Integer, ByVal x As Integer, ByVal y As Integer)
        Dim i As Integer

        ' Check for subscript out of range
        If itemNum < 1 Or itemNum > MAX_ITEMS Or MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Then Exit Sub

        ' Find open map item slot
        i = FindOpenMapItemSlot(MapNum)

        If i = 0 Then Exit Sub

        SpawnItemSlot(i, itemNum, ItemVal, MapNum, x, y)
    End Sub

    Sub SpawnItemSlot(ByVal MapItemSlot As Integer, ByVal itemNum As Integer, ByVal ItemVal As Integer, ByVal MapNum As Integer, ByVal x As Integer, ByVal y As Integer)
        Dim i As Integer
        Dim Buffer As New ByteBuffer

        ' Check for subscript out of range
        If MapItemSlot <= 0 Or MapItemSlot > MAX_MAP_ITEMS Or itemNum < 0 Or itemNum > MAX_ITEMS Or MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Then Exit Sub

        i = MapItemSlot

        If i <> 0 Then
            If itemNum >= 0 And itemNum <= MAX_ITEMS Then
                MapItem(MapNum, i).Num = itemNum
                MapItem(MapNum, i).Value = ItemVal
                MapItem(MapNum, i).X = x
                MapItem(MapNum, i).Y = y

                Buffer.WriteInteger(ServerPackets.SSpawnItem)
                Buffer.WriteInteger(i)
                Buffer.WriteInteger(itemNum)
                Buffer.WriteInteger(ItemVal)
                Buffer.WriteInteger(x)
                Buffer.WriteInteger(y)

                Addlog("Sent SMSG: SSpawnItem MapItemSlot", PACKET_LOG)
                TextAdd("Sent SMSG: SSpawnItem MapItemSlot")

                SendDataToMap(MapNum, Buffer.ToArray())
            End If

        End If

        Buffer = Nothing
    End Sub

    Function FindOpenMapItemSlot(ByVal MapNum As Integer) As Integer
        Dim i As Integer
        FindOpenMapItemSlot = 0

        ' Check for subscript out of range
        If MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Then Exit Function

        For i = 1 To MAX_MAP_ITEMS
            If MapItem(MapNum, i).Num = 0 Then
                FindOpenMapItemSlot = i
                Exit Function
            End If
        Next

    End Function

    Sub SpawnAllMapsItems()
        Dim i As Integer

        For i = 1 To MAX_CACHED_MAPS
            SpawnMapItems(i)
        Next

    End Sub

    Sub SpawnMapItems(ByVal MapNum As Integer)
        Dim x As Integer
        Dim y As Integer

        ' Check for subscript out of range
        If MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Then Exit Sub

        ' Spawn what we have
        For x = 0 To Map(MapNum).MaxX
            For y = 0 To Map(MapNum).MaxY
                ' Check if the tile type is an item or a saved tile incase someone drops something
                If (Map(MapNum).Tile(x, y).Type = TileType.Item) Then

                    ' Check to see if its a currency and if they set the value to 0 set it to 1 automatically
                    If Item(Map(MapNum).Tile(x, y).Data1).Type = ItemType.Currency Or Item(Map(MapNum).Tile(x, y).Data1).Stackable = 1 Then
                        If Map(MapNum).Tile(x, y).Data2 <= 0 Then
                            SpawnItem(Map(MapNum).Tile(x, y).Data1, 1, MapNum, x, y)
                        Else
                            SpawnItem(Map(MapNum).Tile(x, y).Data1, Map(MapNum).Tile(x, y).Data2, MapNum, x, y)
                        End If
                    Else
                        SpawnItem(Map(MapNum).Tile(x, y).Data1, Map(MapNum).Tile(x, y).Data2, MapNum, x, y)
                    End If
                End If
            Next
        Next

    End Sub

    Public Sub SpawnNpc(ByVal MapNpcNum As Integer, ByVal MapNum As Integer)
        Dim Buffer As New ByteBuffer
        Dim NpcNum As Integer
        Dim i As Integer
        Dim x As Integer
        Dim y As Integer
        Dim Spawned As Boolean

        ' Check for subscript out of range
        If MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Then Exit Sub

        NpcNum = Map(MapNum).Npc(MapNpcNum)

        If NpcNum > 0 Then
            If Not Npc(NpcNum).SpawnTime = Time.Instance.TimeOfDay And Not Npc(NpcNum).SpawnTime = 4 Then Exit Sub

            MapNpc(MapNum).Npc(MapNpcNum).Num = NpcNum
            MapNpc(MapNum).Npc(MapNpcNum).Target = 0
            MapNpc(MapNum).Npc(MapNpcNum).TargetType = 0 ' clear

            MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.HP) = GetNpcMaxVital(NpcNum, Vitals.HP)
            MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.MP) = GetNpcMaxVital(NpcNum, Vitals.MP)
            MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.SP) = GetNpcMaxVital(NpcNum, Vitals.SP)

            MapNpc(MapNum).Npc(MapNpcNum).Dir = Int(Rnd() * 4)

            'Check if theres a spawn tile for the specific npc
            For x = 0 To Map(MapNum).MaxX
                For y = 0 To Map(MapNum).MaxY
                    If Map(MapNum).Tile(x, y).Type = TileType.NpcSpawn Then
                        If Map(MapNum).Tile(x, y).Data1 = MapNpcNum Then
                            MapNpc(MapNum).Npc(MapNpcNum).X = x
                            MapNpc(MapNum).Npc(MapNpcNum).Y = y
                            MapNpc(MapNum).Npc(MapNpcNum).Dir = Map(MapNum).Tile(x, y).Data2
                            Spawned = True
                            Exit For
                        End If
                    End If
                Next y
            Next x

            If Not Spawned Then
                ' Well try 100 times to randomly place the sprite
                For i = 1 To 100
                    x = Random(0, Map(MapNum).MaxX)
                    y = Random(0, Map(MapNum).MaxY)

                    If x > Map(MapNum).MaxX Then x = Map(MapNum).MaxX
                    If y > Map(MapNum).MaxY Then y = Map(MapNum).MaxY

                    ' Check if the tile is walkable
                    If NpcTileIsOpen(MapNum, x, y) Then
                        MapNpc(MapNum).Npc(MapNpcNum).X = x
                        MapNpc(MapNum).Npc(MapNpcNum).Y = y
                        Spawned = True
                        Exit For
                    End If
                Next
            End If

            ' Didn't spawn, so now we'll just try to find a free tile
            If Not Spawned Then
                For x = 0 To Map(MapNum).MaxX
                    For y = 0 To Map(MapNum).MaxY
                        If NpcTileIsOpen(MapNum, x, y) Then
                            MapNpc(MapNum).Npc(MapNpcNum).X = x
                            MapNpc(MapNum).Npc(MapNpcNum).Y = y
                            Spawned = True
                        End If
                    Next
                Next
            End If

            ' If we suceeded in spawning then send it to everyone
            If Spawned Then
                Buffer.WriteInteger(ServerPackets.SSpawnNpc)
                Buffer.WriteInteger(MapNpcNum)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Num)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).X)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Y)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Dir)

                Addlog("Recieved SMSG: SSpawnNpc", PACKET_LOG)
                TextAdd("Recieved SMSG: SSpawnNpc")

                For i = 1 To Vitals.Count - 1
                    Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Vital(i))
                Next

                SendDataToMap(MapNum, Buffer.ToArray())
            End If

            SendMapNpcVitals(MapNum, MapNpcNum)
        End If

        Buffer = Nothing
    End Sub

    Public Function Random(ByVal low As Int32, ByVal high As Int32) As Integer
        Static RandomNumGen As New System.Random
        Return RandomNumGen.Next(low, high + 1)
    End Function

    Public Function NpcTileIsOpen(ByVal MapNum As Integer, ByVal x As Integer, ByVal y As Integer) As Boolean
        Dim LoopI As Integer
        NpcTileIsOpen = True

        If PlayersOnMap(MapNum) Then
            For LoopI = 1 To MAX_PLAYERS
                If GetPlayerMap(LoopI) = MapNum AndAlso GetPlayerX(LoopI) = x AndAlso GetPlayerY(LoopI) = y Then
                    NpcTileIsOpen = False
                    Exit Function
                End If
            Next
        End If

        For LoopI = 1 To MAX_MAP_NPCS
            If MapNpc(MapNum).Npc(LoopI).Num > 0 AndAlso MapNpc(MapNum).Npc(LoopI).X = x AndAlso MapNpc(MapNum).Npc(LoopI).Y = y Then
                NpcTileIsOpen = False
                Exit Function
            End If
        Next

        If Map(MapNum).Tile(x, y).Type <> TileType.None AndAlso Map(MapNum).Tile(x, y).Type <> TileType.NpcSpawn AndAlso Map(MapNum).Tile(x, y).Type <> TileType.Item Then
            NpcTileIsOpen = False
        End If

    End Function

    Public Function CheckGrammar(ByVal Word As String, Optional ByVal Caps As Byte = 0) As String
        Dim FirstLetter As String

        FirstLetter = LCase$(Left$(Word, 1))

        If FirstLetter = "$" Then
            CheckGrammar = (Mid$(Word, 2, Len(Word) - 1))
            Exit Function
        End If

        If FirstLetter Like "*[aeiou]*" Then
            If Caps Then CheckGrammar = "An " & Word Else CheckGrammar = "an " & Word
        Else
            If Caps Then CheckGrammar = "A " & Word Else CheckGrammar = "a " & Word
        End If
    End Function

    Function CanNpcMove(ByVal MapNum As Integer, ByVal MapNpcNum As Integer, ByVal Dir As Byte) As Boolean
        Dim i As Integer
        Dim n As Integer
        Dim x As Integer
        Dim y As Integer

        ' Check for subscript out of range
        If MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Dir < Direction.Up Or Dir > Direction.Right Then
            Exit Function
        End If

        x = MapNpc(MapNum).Npc(MapNpcNum).X
        y = MapNpc(MapNum).Npc(MapNpcNum).Y
        CanNpcMove = True

        Select Case Dir
            Case Direction.Up

                ' Check to make sure not outside of boundries
                If y > 0 Then
                    n = Map(MapNum).Tile(x, y - 1).Type

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To GetPlayersOnline()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = MapNum) And (GetPlayerX(i) = MapNpc(MapNum).Npc(MapNpcNum).X) And (GetPlayerY(i) = MapNpc(MapNum).Npc(MapNpcNum).Y - 1) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNpc(MapNum).Npc(i).Num > 0) And (MapNpc(MapNum).Npc(i).X = MapNpc(MapNum).Npc(MapNpcNum).X) And (MapNpc(MapNum).Npc(i).Y = MapNpc(MapNum).Npc(MapNpcNum).Y - 1) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

            Case Direction.Down

                ' Check to make sure not outside of boundries
                If y < Map(MapNum).MaxY Then
                    n = Map(MapNum).Tile(x, y + 1).Type

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To GetPlayersOnline()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = MapNum) And (GetPlayerX(i) = MapNpc(MapNum).Npc(MapNpcNum).X) And (GetPlayerY(i) = MapNpc(MapNum).Npc(MapNpcNum).Y + 1) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNpc(MapNum).Npc(i).Num > 0) And (MapNpc(MapNum).Npc(i).X = MapNpc(MapNum).Npc(MapNpcNum).X) And (MapNpc(MapNum).Npc(i).Y = MapNpc(MapNum).Npc(MapNpcNum).Y + 1) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

            Case Direction.Left

                ' Check to make sure not outside of boundries
                If x > 0 Then
                    n = Map(MapNum).Tile(x - 1, y).Type

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To GetPlayersOnline()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = MapNum) And (GetPlayerX(i) = MapNpc(MapNum).Npc(MapNpcNum).X - 1) And (GetPlayerY(i) = MapNpc(MapNum).Npc(MapNpcNum).Y) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNpc(MapNum).Npc(i).Num > 0) And (MapNpc(MapNum).Npc(i).X = MapNpc(MapNum).Npc(MapNpcNum).X - 1) And (MapNpc(MapNum).Npc(i).Y = MapNpc(MapNum).Npc(MapNpcNum).Y) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

            Case Direction.Right

                ' Check to make sure not outside of boundries
                If x < Map(MapNum).MaxX Then
                    n = Map(MapNum).Tile(x + 1, y).Type

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To GetPlayersOnline()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = MapNum) And (GetPlayerX(i) = MapNpc(MapNum).Npc(MapNpcNum).X + 1) And (GetPlayerY(i) = MapNpc(MapNum).Npc(MapNpcNum).Y) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNpc(MapNum).Npc(i).Num > 0) And (MapNpc(MapNum).Npc(i).X = MapNpc(MapNum).Npc(MapNpcNum).X + 1) And (MapNpc(MapNum).Npc(i).Y = MapNpc(MapNum).Npc(MapNpcNum).Y) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

        End Select

        If MapNpc(MapNum).Npc(MapNpcNum).SkillBuffer > 0 Then CanNpcMove = False

    End Function

    Sub NpcMove(ByVal MapNum As Integer, ByVal MapNpcNum As Integer, ByVal Dir As Integer, ByVal Movement As Integer)
        Dim Buffer As New ByteBuffer

        ' Check for subscript out of range
        If MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Dir < Direction.Up Or Dir > Direction.Right Or Movement < 1 Or Movement > 2 Then
            Exit Sub
        End If

        MapNpc(MapNum).Npc(MapNpcNum).Dir = Dir

        Select Case Dir
            Case Direction.Up
                MapNpc(MapNum).Npc(MapNpcNum).Y = MapNpc(MapNum).Npc(MapNpcNum).Y - 1

                Buffer.WriteInteger(ServerPackets.SNpcMove)
                Buffer.WriteInteger(MapNpcNum)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).X)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Y)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Dir)
                Buffer.WriteInteger(Movement)

                Addlog("Sent SMSG: SNpcMove Up", PACKET_LOG)
                TextAdd("Sent SMSG: SNpcMove Up")

                SendDataToMap(MapNum, Buffer.ToArray())
            Case Direction.Down
                MapNpc(MapNum).Npc(MapNpcNum).Y = MapNpc(MapNum).Npc(MapNpcNum).Y + 1

                Buffer.WriteInteger(ServerPackets.SNpcMove)
                Buffer.WriteInteger(MapNpcNum)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).X)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Y)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Dir)
                Buffer.WriteInteger(Movement)

                Addlog("Sent SMSG: SNpcMove Down", PACKET_LOG)
                TextAdd("Sent SMSG: SNpcMove Down")

                SendDataToMap(MapNum, Buffer.ToArray())
            Case Direction.Left
                MapNpc(MapNum).Npc(MapNpcNum).X = MapNpc(MapNum).Npc(MapNpcNum).X - 1

                Buffer.WriteInteger(ServerPackets.SNpcMove)
                Buffer.WriteInteger(MapNpcNum)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).X)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Y)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Dir)
                Buffer.WriteInteger(Movement)

                Addlog("Sent SMSG: SNpcMove Left", PACKET_LOG)
                TextAdd("Sent SMSG: SNpcMove Left")

                SendDataToMap(MapNum, Buffer.ToArray())
            Case Direction.Right
                MapNpc(MapNum).Npc(MapNpcNum).X = MapNpc(MapNum).Npc(MapNpcNum).X + 1

                Buffer.WriteInteger(ServerPackets.SNpcMove)
                Buffer.WriteInteger(MapNpcNum)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).X)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Y)
                Buffer.WriteInteger(MapNpc(MapNum).Npc(MapNpcNum).Dir)
                Buffer.WriteInteger(Movement)

                Addlog("Sent SMSG: SNpcMove Right", PACKET_LOG)
                TextAdd("Sent SMSG: SNpcMove Right")

                SendDataToMap(MapNum, Buffer.ToArray())
        End Select

        Buffer = Nothing
    End Sub

    Sub NpcDir(ByVal MapNum As Integer, ByVal MapNpcNum As Integer, ByVal Dir As Integer)
        Dim Buffer As New ByteBuffer

        ' Check for subscript out of range
        If MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Dir < Direction.Up Or Dir > Direction.Right Then
            Exit Sub
        End If

        MapNpc(MapNum).Npc(MapNpcNum).Dir = Dir

        Buffer.WriteInteger(ServerPackets.SNpcDir)
        Buffer.WriteInteger(MapNpcNum)
        Buffer.WriteInteger(Dir)

        Addlog("Sent SMSG: SNpcDir", PACKET_LOG)
        TextAdd("Sent SMSG: SNpcDir")

        SendDataToMap(MapNum, Buffer.ToArray())

        Buffer = Nothing
    End Sub

    Sub SpawnAllMapNpcs()
        Dim i As Integer

        For i = 1 To MAX_CACHED_MAPS
            SpawnMapNpcs(i)
        Next

    End Sub

    Sub SpawnMapNpcs(ByVal MapNum As Integer)
        Dim i As Integer

        For i = 1 To MAX_MAP_NPCS
            SpawnNpc(i, MapNum)
        Next

    End Sub

    Sub SendMapNpcsToMap(ByVal MapNum As Integer)
        Dim i As Integer
        Dim Buffer As New ByteBuffer

        Buffer.WriteInteger(ServerPackets.SMapNpcData)

        Addlog("Sent SMSG: SMapNpcData", PACKET_LOG)
        TextAdd("Sent SMSG: SMapNpcData")

        For i = 1 To MAX_MAP_NPCS
            Buffer.WriteInteger(MapNpc(MapNum).Npc(i).Num)
            Buffer.WriteInteger(MapNpc(MapNum).Npc(i).X)
            Buffer.WriteInteger(MapNpc(MapNum).Npc(i).Y)
            Buffer.WriteInteger(MapNpc(MapNum).Npc(i).Dir)
            Buffer.WriteInteger(MapNpc(MapNum).Npc(i).Vital(Vitals.HP))
            Buffer.WriteInteger(MapNpc(MapNum).Npc(i).Vital(Vitals.MP))
        Next

        SendDataToMap(MapNum, Buffer.ToArray())

        Buffer = Nothing
    End Sub
End Module