﻿Imports System.Linq

Module ServerPlayers
#Region "PlayerCombat"
    Function CanPlayerAttackPlayer(ByVal Attacker As Integer, ByVal Victim As Integer, Optional ByVal IsSkill As Boolean = False) As Boolean

        If Not IsSkill Then
            ' Check attack timer
            If GetPlayerEquipment(Attacker, EquipmentType.Weapon) > 0 Then
                If GetTimeMs() < TempPlayer(Attacker).AttackTimer + Item(GetPlayerEquipment(Attacker, EquipmentType.Weapon)).Speed Then Exit Function
            Else
                If GetTimeMs() < TempPlayer(Attacker).AttackTimer + 1000 Then Exit Function
            End If
        End If

        ' Check for subscript out of range
        If Not IsPlaying(Victim) Then Exit Function

        ' Make sure they are on the same map
        If Not GetPlayerMap(Attacker) = GetPlayerMap(Victim) Then Exit Function

        ' Make sure we dont attack the player if they are switching maps
        If TempPlayer(Victim).GettingMap = True Then Exit Function

        If Not IsSkill Then
            ' Check if at same coordinates
            Select Case GetPlayerDir(Attacker)
                Case Direction.Up

                    If Not ((GetPlayerY(Victim) + 1 = GetPlayerY(Attacker)) And (GetPlayerX(Victim) = GetPlayerX(Attacker))) Then Exit Function
                Case Direction.Down

                    If Not ((GetPlayerY(Victim) - 1 = GetPlayerY(Attacker)) And (GetPlayerX(Victim) = GetPlayerX(Attacker))) Then Exit Function
                Case Direction.Left

                    If Not ((GetPlayerY(Victim) = GetPlayerY(Attacker)) And (GetPlayerX(Victim) + 1 = GetPlayerX(Attacker))) Then Exit Function
                Case Direction.Right

                    If Not ((GetPlayerY(Victim) = GetPlayerY(Attacker)) And (GetPlayerX(Victim) - 1 = GetPlayerX(Attacker))) Then Exit Function
                Case Else
                    Exit Function
            End Select
        End If

        ' Check if map is attackable
        If Not Map(GetPlayerMap(Attacker)).Moral = MapMoral.None Then
            If GetPlayerPK(Victim) = False Then
                PlayerMsg(Attacker, "This is a safe zone!", ColorType.BrightRed)
                Exit Function
            End If
        End If

        ' Make sure they have more then 0 hp
        If GetPlayerVital(Victim, Vitals.HP) <= 0 Then Exit Function

        ' Check to make sure that they dont have access
        If GetPlayerAccess(Attacker) > AdminType.Monitor Then
            PlayerMsg(Attacker, "You cannot attack any player for thou art an admin!", ColorType.BrightRed)
            Exit Function
        End If

        ' Check to make sure the victim isn't an admin
        If GetPlayerAccess(Victim) > AdminType.Monitor Then
            PlayerMsg(Attacker, "You cannot attack " & GetPlayerName(Victim) & "!", ColorType.BrightRed)
            Exit Function
        End If

        ' Make sure attacker is high enough level
        If GetPlayerLevel(Attacker) < 10 Then
            PlayerMsg(Attacker, "You are below level 10, you cannot attack another player yet!", ColorType.BrightRed)
            Exit Function
        End If

        ' Make sure victim is high enough level
        If GetPlayerLevel(Victim) < 10 Then
            PlayerMsg(Attacker, GetPlayerName(Victim) & " is below level 10, you cannot attack this player yet!", ColorType.BrightRed)
            Exit Function
        End If

        CanPlayerAttackPlayer = True
    End Function

    Function CanPlayerBlockHit(ByVal Index As Integer) As Boolean
        Dim i As Integer
        Dim n As Integer
        Dim ShieldSlot As Integer
        ShieldSlot = GetPlayerEquipment(Index, EquipmentType.Shield)

        CanPlayerBlockHit = False

        If ShieldSlot > 0 Then
            n = Int(Rnd() * 2)

            If n = 1 Then
                i = (GetPlayerStat(Index, Stats.Endurance) \ 2) + (GetPlayerLevel(Index) \ 2)
                n = Int(Rnd() * 100) + 1

                If n <= i Then
                    CanPlayerBlockHit = True
                End If
            End If
        End If

    End Function

    Function CanPlayerCriticalHit(ByVal Index As Integer) As Boolean
        On Error Resume Next
        Dim i As Integer
        Dim n As Integer

        If GetPlayerEquipment(Index, EquipmentType.Weapon) > 0 Then
            n = (Rnd()) * 2

            If n = 1 Then
                i = (GetPlayerStat(Index, Stats.Strength) \ 2) + (GetPlayerLevel(Index) \ 2)
                n = Int(Rnd() * 100) + 1

                If n <= i Then
                    CanPlayerCriticalHit = True
                End If
            End If
        End If

    End Function

    Function GetPlayerDamage(ByVal Index As Integer) As Integer
        Dim weaponNum As Integer

        GetPlayerDamage = 0

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or Index <= 0 Or Index > MAX_PLAYERS Then
            Exit Function
        End If

        If GetPlayerEquipment(Index, EquipmentType.Weapon) > 0 Then
            weaponNum = GetPlayerEquipment(Index, EquipmentType.Weapon)
            GetPlayerDamage = (GetPlayerStat(Index, Stats.Strength) * 2) + (Item(weaponNum).Data2 * 2) + (GetPlayerLevel(Index) * 3) + Random(0, 20)
        Else
            GetPlayerDamage = (GetPlayerStat(Index, Stats.Strength) * 2) + (GetPlayerLevel(Index) * 3) + Random(0, 20)
        End If

    End Function

    Function GetPlayerProtection(ByVal Index As Integer) As Integer
        Dim Armor As Integer, Helm As Integer, Shoes As Integer, Gloves As Integer
        GetPlayerProtection = 0

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or Index <= 0 Or Index > MAX_PLAYERS Then
            Exit Function
        End If

        Armor = GetPlayerEquipment(Index, EquipmentType.Armor)
        Helm = GetPlayerEquipment(Index, EquipmentType.Helmet)
        Shoes = GetPlayerEquipment(Index, EquipmentType.Shoes)
        Gloves = GetPlayerEquipment(Index, EquipmentType.Gloves)
        GetPlayerProtection = (GetPlayerStat(Index, Stats.Endurance) \ 5)

        If Armor > 0 Then
            GetPlayerProtection = GetPlayerProtection + Item(Armor).Data2
        End If

        If Helm > 0 Then
            GetPlayerProtection = GetPlayerProtection + Item(Helm).Data2
        End If

        If Shoes > 0 Then
            GetPlayerProtection = GetPlayerProtection + Item(Shoes).Data2
        End If

        If Gloves > 0 Then
            GetPlayerProtection = GetPlayerProtection + Item(Gloves).Data2
        End If

    End Function

    Sub AttackPlayer(ByVal Attacker As Integer, ByVal Victim As Integer, ByVal Damage As Integer, Optional ByVal skillnum As Integer = 0, Optional ByVal npcnum As Byte = 0)
        Dim exp As Integer, mapnum As Integer
        Dim n As Integer
        Dim Buffer As ByteBuffer

        If npcnum = 0 Then
            ' Check for subscript out of range
            If IsPlaying(Attacker) = False Or IsPlaying(Victim) = False Or Damage < 0 Then
                Exit Sub
            End If

            ' Check for weapon
            n = 0

            If GetPlayerEquipment(Attacker, EquipmentType.Weapon) > 0 Then
                n = GetPlayerEquipment(Attacker, EquipmentType.Weapon)
            End If

            ' Send this packet so they can see the person attacking
            Buffer = New ByteBuffer
            Buffer.WriteInteger(ServerPackets.SAttack)
            Buffer.WriteInteger(Attacker)
            SendDataToMapBut(Attacker, GetPlayerMap(Attacker), Buffer.ToArray())
            Buffer = Nothing

            If Damage >= GetPlayerVital(Victim, Vitals.HP) Then

                SendActionMsg(GetPlayerMap(Victim), "-" & Damage, ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))

                ' Player is dead
                GlobalMsg(GetPlayerName(Victim) & " has been killed by " & GetPlayerName(Attacker))
                ' Calculate exp to give attacker
                exp = (GetPlayerExp(Victim) \ 10)

                ' Make sure we dont get less then 0
                If exp < 0 Then
                    exp = 0
                End If

                If exp = 0 Then
                    PlayerMsg(Victim, "You lost no exp.", ColorType.BrightGreen)
                    PlayerMsg(Attacker, "You received no exp.", ColorType.BrightRed)
                Else
                    SetPlayerExp(Victim, GetPlayerExp(Victim) - exp)
                    SendExp(Victim)
                    PlayerMsg(Victim, "You lost " & exp & " exp.", ColorType.BrightRed)
                    SetPlayerExp(Attacker, GetPlayerExp(Attacker) + exp)
                    SendExp(Attacker)
                    PlayerMsg(Attacker, "You received " & exp & " exp.", ColorType.BrightGreen)
                End If

                ' Check for a level up
                CheckPlayerLevelUp(Attacker)

                ' Check if target is player who died and if so set target to 0
                If TempPlayer(Attacker).TargetType = TargetType.Player Then
                    If TempPlayer(Attacker).Target = Victim Then
                        TempPlayer(Attacker).Target = 0
                        TempPlayer(Attacker).TargetType = TargetType.None
                    End If
                End If

                If GetPlayerPK(Victim) = False Then
                    If GetPlayerPK(Attacker) = False Then
                        SetPlayerPK(Attacker, True)
                        SendPlayerData(Attacker)
                        GlobalMsg(GetPlayerName(Attacker) & " has been deemed a Player Killer!!!")
                    End If

                Else
                    GlobalMsg(GetPlayerName(Victim) & " has paid the price for being a Player Killer!!!")
                End If

                OnDeath(Victim)
            Else
                ' Player not dead, just do the damage
                SetPlayerVital(Victim, Vitals.HP, GetPlayerVital(Victim, Vitals.HP) - Damage)
                SendVital(Victim, Vitals.HP)
                SendActionMsg(GetPlayerMap(Victim), "-" & Damage, ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))

                'if a stunning skill, stun the player
                If skillnum > 0 Then
                    If Skill(skillnum).StunDuration > 0 Then StunPlayer(Victim, skillnum)
                End If
            End If

            ' Reset attack timer
            TempPlayer(Attacker).AttackTimer = GetTimeMs()
        Else ' npc to player
            ' Check for subscript out of range
            If IsPlaying(Victim) = False Or Damage < 0 Then Exit Sub

            mapnum = GetPlayerMap(Victim)

            ' Send this packet so they can see the person attacking
            Buffer = New ByteBuffer
            Buffer.WriteInteger(ServerPackets.SNpcAttack)
            Buffer.WriteInteger(Attacker)
            SendDataToMap(mapnum, Buffer.ToArray())
            Buffer = Nothing

            If Damage >= GetPlayerVital(Victim, Vitals.HP) Then

                SendActionMsg(mapnum, "-" & Damage, ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))

                ' Player is dead
                GlobalMsg(GetPlayerName(Victim) & " has been killed by " & Npc(MapNpc(mapnum).Npc(Attacker).Num).Name)

                ' Check if target is player who died and if so set target to 0
                If TempPlayer(Attacker).TargetType = TargetType.Player Then
                    If TempPlayer(Attacker).Target = Victim Then
                        TempPlayer(Attacker).Target = 0
                        TempPlayer(Attacker).TargetType = TargetType.None
                    End If
                End If

                OnDeath(Victim)
            Else
                ' Player not dead, just do the damage
                SetPlayerVital(Victim, Vitals.HP, GetPlayerVital(Victim, Vitals.HP) - Damage)
                SendVital(Victim, Vitals.HP)
                SendActionMsg(mapnum, "-" & Damage, ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))

                'if a stunning skill, stun the player
                If skillnum > 0 Then
                    If Skill(skillnum).StunDuration > 0 Then StunPlayer(Victim, skillnum)
                End If
            End If

            ' Reset attack timer
            MapNpc(mapnum).Npc(Attacker).AttackTimer = GetTimeMs()
        End If

    End Sub

    Public Sub StunPlayer(ByVal Index As Integer, ByVal skillnum As Integer)
        ' check if it's a stunning skill
        If Skill(skillnum).StunDuration > 0 Then
            ' set the values on index
            TempPlayer(Index).StunDuration = Skill(skillnum).StunDuration
            TempPlayer(Index).StunTimer = GetTimeMs()
            ' send it to the index
            SendStunned(Index)
            ' tell him he's stunned
            PlayerMsg(Index, "You have been stunned!", ColorType.Yellow)
        End If
    End Sub

    Function CanPlayerAttackNpc(ByVal Attacker As Integer, ByVal MapNpcNum As Integer, Optional ByVal IsSkill As Boolean = False) As Boolean
        Dim MapNum As Integer
        Dim NpcNum As Integer
        Dim atkX As Integer
        Dim atkY As Integer
        Dim attackspeed As Integer

        ' Check for subscript out of range
        If IsPlaying(Attacker) = False Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Then
            Exit Function
        End If

        ' Check for subscript out of range
        If MapNpc(GetPlayerMap(Attacker)).Npc(MapNpcNum).Num <= 0 Then
            Exit Function
        End If

        MapNum = GetPlayerMap(Attacker)
        NpcNum = MapNpc(MapNum).Npc(MapNpcNum).Num

        ' Make sure the npc isn't already dead
        If MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.HP) <= 0 Then
            Exit Function
        End If

        ' Make sure they are on the same map
        If IsPlaying(Attacker) Then

            ' attack speed from weapon
            If GetPlayerEquipment(Attacker, EquipmentType.Weapon) > 0 Then
                attackspeed = Item(GetPlayerEquipment(Attacker, EquipmentType.Weapon)).Speed
            Else
                attackspeed = 1000
            End If

            If NpcNum > 0 And GetTimeMs() > TempPlayer(Attacker).AttackTimer + attackspeed Then

                ' exit out early
                If IsSkill Then
                    If Npc(NpcNum).Behaviour <> NpcBehavior.Friendly And Npc(NpcNum).Behaviour <> NpcBehavior.ShopKeeper Then
                        CanPlayerAttackNpc = True
                        Exit Function
                    End If
                End If

                ' Check if at same coordinates
                Select Case GetPlayerDir(Attacker)
                    Case Direction.Up
                        atkX = GetPlayerX(Attacker)
                        atkY = GetPlayerY(Attacker) - 1
                    Case Direction.Down
                        atkX = GetPlayerX(Attacker)
                        atkY = GetPlayerY(Attacker) + 1
                    Case Direction.Left
                        atkX = GetPlayerX(Attacker) - 1
                        atkY = GetPlayerY(Attacker)
                    Case Direction.Right
                        atkX = GetPlayerX(Attacker) + 1
                        atkY = GetPlayerY(Attacker)
                End Select

                If atkX = MapNpc(MapNum).Npc(MapNpcNum).X Then
                    If atkY = MapNpc(MapNum).Npc(MapNpcNum).Y Then
                        If Npc(NpcNum).Behaviour <> NpcBehavior.Friendly And Npc(NpcNum).Behaviour <> NpcBehavior.ShopKeeper And Npc(NpcNum).Behaviour <> NpcBehavior.Quest Then
                            CanPlayerAttackNpc = True
                        Else
                            If Npc(NpcNum).Behaviour = NpcBehavior.Quest Then
                                If QuestCompleted(Attacker, Npc(NpcNum).QuestNum) Then
                                    PlayerMsg(Attacker, Trim$(Npc(NpcNum).Name) & ": " & Trim$(Npc(NpcNum).AttackSay), ColorType.Yellow)
                                    Exit Function
                                ElseIf Not CanStartQuest(Attacker, Npc(NpcNum).QuestNum) And Not QuestInProgress(Attacker, Npc(NpcNum).QuestNum) Then
                                    CheckTasks(Attacker, QuestType.Talk, NpcNum)
                                    CheckTasks(Attacker, QuestType.Give, NpcNum)
                                    CheckTasks(Attacker, QuestType.Fetch, NpcNum)
                                    Exit Function
                                ElseIf QuestInProgress(Attacker, Npc(NpcNum).QuestNum) Then
                                    CheckTasks(Attacker, QuestType.Talk, NpcNum)
                                    CheckTasks(Attacker, QuestType.Give, NpcNum)
                                    CheckTasks(Attacker, QuestType.Fetch, NpcNum)
                                    Exit Function
                                Else
                                    ShowQuest(Attacker, Npc(NpcNum).QuestNum)
                                    Exit Function
                                End If
                            ElseIf Npc(NpcNum).Behaviour = NpcBehavior.Friendly Or Npc(NpcNum).Behaviour = NpcBehavior.ShopKeeper Then
                                CheckTasks(Attacker, QuestType.Talk, NpcNum)
                                CheckTasks(Attacker, QuestType.Give, NpcNum)
                                CheckTasks(Attacker, QuestType.Fetch, NpcNum)
                                'Exit Function
                            End If
                            If Len(Trim$(Npc(NpcNum).AttackSay)) > 0 Then
                                PlayerMsg(Attacker, Trim$(Npc(NpcNum).Name) & ": " & Trim$(Npc(NpcNum).AttackSay), ColorType.Yellow)
                            End If
                        End If
                    End If
                End If
            End If
        End If

    End Function

    Public Sub StunNPC(ByVal Index As Integer, ByVal MapNum As Integer, ByVal skillnum As Integer)
        ' check if it's a stunning skill
        If Skill(skillnum).StunDuration > 0 Then
            ' set the values on index
            MapNpc(MapNum).Npc(Index).StunDuration = Skill(skillnum).StunDuration
            MapNpc(MapNum).Npc(Index).StunTimer = GetTimeMs()
        End If
    End Sub

    Sub PlayerAttackNpc(ByVal Attacker As Integer, ByVal MapNpcNum As Integer, ByVal Damage As Integer)
        ' Check for subscript out of range
        If IsPlaying(Attacker) = False Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Damage < 0 Then Exit Sub

        Dim MapNum = GetPlayerMap(Attacker)
        Dim NpcNum = MapNpc(MapNum).Npc(MapNpcNum).Num
        Dim Name = Npc(NpcNum).Name.Trim()

        ' Check for weapon
        Dim Weapon = 0
        If GetPlayerEquipment(Attacker, EquipmentType.Weapon) > 0 Then
            Weapon = GetPlayerEquipment(Attacker, EquipmentType.Weapon)
        End If

        ' Deal damage to our NPC.
        MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.HP) = MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.HP) - Damage

        ' Set the NPC target to the player so they can come after them.
        MapNpc(MapNum).Npc(MapNpcNum).TargetType = TargetType.Player
        MapNpc(MapNum).Npc(MapNpcNum).Target = Attacker

        ' Check for any mobs on the map with the Guard behaviour so they can come after our player.
        If Npc(MapNpc(MapNum).Npc(MapNpcNum).Num).Behaviour = NpcBehavior.Guard Then
            For Each Guard In MapNpc(MapNum).Npc.Where(Function(x) x.Num = MapNpc(MapNum).Npc(MapNpcNum).Num).Select(Function(x, y) y + 1).ToArray()
                MapNpc(MapNum).Npc(Guard).Target = Attacker
                MapNpc(MapNum).Npc(Guard).TargetType = TargetType.Player
            Next
        End If

        ' Send our general visual stuff.
        SendActionMsg(MapNum, "-" & Damage, ColorType.BrightRed, 1, (MapNpc(MapNum).Npc(MapNpcNum).X * 32), (MapNpc(MapNum).Npc(MapNpcNum).Y * 32))
        SendBlood(GetPlayerMap(Attacker), MapNpc(MapNum).Npc(MapNpcNum).X, MapNpc(MapNum).Npc(MapNpcNum).Y)
        SendPlayerAttack(Attacker)
        If Weapon > 0 Then
            SendAnimation(MapNum, Item(GetPlayerEquipment(Attacker, EquipmentType.Weapon)).Animation, 0, 0, TargetType.Npc, MapNpcNum)
        End If

        ' Reset our attack timer.
        TempPlayer(Attacker).AttackTimer = GetTimeMs()

        If Not IsNpcDead(MapNum, MapNpcNum) Then
            ' Check if our NPC has something to share with our player.
            If MapNpc(MapNum).Npc(MapNpcNum).Target = 0 Then
                If Len(Trim$(Npc(NpcNum).AttackSay)) > 0 Then
                    PlayerMsg(Attacker, String.Format("{0} says: '{1}'", Npc(NpcNum).Name.Trim(), Npc(NpcNum).AttackSay.Trim()), ColorType.Yellow)
                End If
            End If

            SendMapNpcTo(MapNum, MapNpcNum)
        Else
            HandlePlayerKillNpc(MapNum, Attacker, MapNpcNum)
        End If
    End Sub


    Function IsInRange(ByVal range As Integer, ByVal x1 As Integer, ByVal y1 As Integer, ByVal x2 As Integer, ByVal y2 As Integer) As Boolean
        Dim nVal As Integer
        IsInRange = False
        nVal = Math.Sqrt((x1 - x2) ^ 2 + (y1 - y2) ^ 2)
        If nVal <= range Then IsInRange = True
    End Function


    Public Sub SpellPlayer_Effect(ByVal Vital As Byte, ByVal increment As Boolean, ByVal Index As Integer, ByVal Damage As Integer, ByVal Skillnum As Integer)
        Dim sSymbol As String
        Dim Colour As Integer

        If Damage > 0 Then
            If increment Then
                sSymbol = "+"
                If Vital = Vitals.HP Then Colour = ColorType.BrightGreen
                If Vital = Vitals.MP Then Colour = ColorType.BrightBlue
            Else
                sSymbol = "-"
                Colour = ColorType.Blue
            End If

            SendAnimation(GetPlayerMap(Index), Skill(Skillnum).SkillAnim, 0, 0, TargetType.Player, Index)
            SendActionMsg(GetPlayerMap(Index), sSymbol & Damage, Colour, ActionMsgType.Scroll, GetPlayerX(Index) * 32, GetPlayerY(Index) * 32)

            ' send the sound
            'SendMapSound Index, GetPlayerX(Index), GetPlayerY(Index), SoundEntity.seSpell, Spellnum

            If increment Then
                SetPlayerVital(Index, Vital, GetPlayerVital(Index, Vital) + Damage)

                If Skill(Skillnum).Duration > 0 Then
                    'AddHoT_Player(Index, Spellnum)
                End If

            ElseIf Not increment Then
                SetPlayerVital(Index, Vital, GetPlayerVital(Index, Vital) - Damage)
            End If

            SendVital(Index, Vital)

        End If

    End Sub

    Public Function CanPlayerDodge(ByVal Index As Integer) As Boolean
        Dim rate As Long, rndNum As Long

        CanPlayerDodge = False

        rate = GetPlayerStat(Index, Stats.Luck) / 4
        rndNum = Random(1, 100)

        If rndNum <= rate Then
            CanPlayerDodge = True
        End If

    End Function

    Public Function CanPlayerParry(ByVal Index As Integer) As Boolean
        Dim rate As Integer, rndNum As Integer

        CanPlayerParry = False

        rate = GetPlayerStat(Index, Stats.Luck) / 6
        rndNum = Random(1, 100)

        If rndNum <= rate Then
            CanPlayerParry = True
        End If

    End Function

    Public Sub TryPlayerAttackPlayer(ByVal Attacker As Integer, ByVal Victim As Integer)
        Dim MapNum As Integer
        Dim Damage As Integer, i As Integer, armor As Integer

        Damage = 0

        ' Can we attack the player?
        If CanPlayerAttackPlayer(Attacker, Victim) Then

            MapNum = GetPlayerMap(Attacker)

            ' check if NPC can avoid the attack
            If CanPlayerDodge(Victim) Then
                SendActionMsg(MapNum, "Dodge!", ColorType.Pink, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))
                Exit Sub
            End If

            If CanPlayerParry(Victim) Then
                SendActionMsg(MapNum, "Parry!", ColorType.Pink, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))
                Exit Sub
            End If

            ' Get the damage we can do
            Damage = GetPlayerDamage(Attacker)

            If CanPlayerBlockHit(Victim) Then
                SendActionMsg(MapNum, "Block!", ColorType.BrightCyan, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))
                Damage = 0
                Exit Sub
            Else

                For i = 1 To EquipmentType.Count - 1
                    If GetPlayerEquipment(Victim, i) > 0 Then
                        armor = armor + Item(GetPlayerEquipment(Victim, i)).Data2
                    End If
                Next

                ' take away armour
                Damage = Damage - ((GetPlayerStat(Victim, Stats.Spirit) * 2) + (GetPlayerLevel(Victim) * 3) + armor)

                ' * 1.5 if it's a crit!
                If CanPlayerCriticalHit(Attacker) Then
                    Damage = Damage * 1.5
                    SendActionMsg(MapNum, "Critical!", ColorType.BrightCyan, 1, (GetPlayerX(Attacker) * 32), (GetPlayerY(Attacker) * 32))
                End If
            End If

            If Damage > 0 Then
                PlayerAttackPlayer(Attacker, Victim, Damage)
            Else
                PlayerMsg(Attacker, "Your attack does nothing.", ColorType.BrightRed)
            End If

        End If

    End Sub

    Sub PlayerAttackPlayer(ByVal Attacker As Integer, ByVal Victim As Integer, ByVal Damage As Integer)
        ' Check for subscript out of range
        If IsPlaying(Attacker) = False Or IsPlaying(Victim) = False Or Damage <= 0 Then
            Exit Sub
        End If

        ' Check if our assailant has a weapon.
        Dim Weapon = 0
        If GetPlayerEquipment(Attacker, EquipmentType.Weapon) > 0 Then
            Weapon = GetPlayerEquipment(Attacker, EquipmentType.Weapon)
        End If

        ' Stop our player's regeneration abilities.
        TempPlayer(Attacker).StopRegen = True
        TempPlayer(Attacker).StopRegenTimer = GetTimeMs()

        ' Deal damage to our player.
        SetPlayerVital(Victim, Vitals.HP, GetPlayerVital(Victim, Vitals.HP) - Damage)

        ' Send all the visuals to our player.
        If Weapon > 0 Then
            SendAnimation(GetPlayerMap(Victim), Item(Weapon).Animation, 0, 0, TargetType.Player, Victim)
        End If
        SendActionMsg(GetPlayerMap(Victim), "-" & Damage, ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))
        SendBlood(GetPlayerMap(Victim), GetPlayerX(Victim), GetPlayerY(Victim))

        ' set the regen timer
        TempPlayer(Victim).StopRegen = True
        TempPlayer(Victim).StopRegenTimer = GetTimeMs()

        ' Reset attack timer
        TempPlayer(Attacker).AttackTimer = GetTimeMs()

        If Not IsPlayerDead(Victim) Then
            ' Send our player's new vitals to everyone that needs them.
            SendVital(Victim, Vitals.HP)
            If TempPlayer(Victim).InParty > 0 Then SendPartyVitals(TempPlayer(Victim).InParty, Victim)
        Else
            ' Handle our dead player.
            HandlePlayerKillPlayer(Attacker, Victim)
        End If
    End Sub

    Public Sub TryPlayerAttackNpc(ByVal Index As Integer, ByVal mapnpcnum As Integer)

        Dim npcnum As Integer

        Dim MapNum As Integer

        Dim Damage As Integer

        Damage = 0

        ' Can we attack the npc?
        If CanPlayerAttackNpc(Index, mapnpcnum) Then

            MapNum = GetPlayerMap(Index)
            npcnum = MapNpc(MapNum).Npc(mapnpcnum).Num

            ' check if NPC can avoid the attack
            If CanNpcDodge(npcnum) Then
                SendActionMsg(MapNum, "Dodge!", ColorType.Pink, 1, (MapNpc(MapNum).Npc(mapnpcnum).X * 32), (MapNpc(MapNum).Npc(mapnpcnum).Y * 32))
                Exit Sub
            End If

            If CanNpcParry(npcnum) Then
                SendActionMsg(MapNum, "Parry!", ColorType.Pink, 1, (MapNpc(MapNum).Npc(mapnpcnum).X * 32), (MapNpc(MapNum).Npc(mapnpcnum).Y * 32))
                Exit Sub
            End If

            ' Get the damage we can do
            Damage = GetPlayerDamage(Index)

            If CanNpcBlock(npcnum) Then
                SendActionMsg(MapNum, "Block!", ColorType.BrightCyan, 1, (MapNpc(MapNum).Npc(mapnpcnum).X * 32), (MapNpc(MapNum).Npc(mapnpcnum).Y * 32))
                Damage = 0
                Exit Sub
            Else

                Damage = Damage - ((Npc(npcnum).Stat(Stats.Spirit) * 2) + (Npc(npcnum).Level * 3))

                ' * 1.5 if it's a crit!
                If CanPlayerCriticalHit(Index) Then
                    Damage = Damage * 1.5
                    SendActionMsg(MapNum, "Critical!", ColorType.BrightCyan, 1, (GetPlayerX(Index) * 32), (GetPlayerY(Index) * 32))
                End If

            End If

            TempPlayer(Index).Target = mapnpcnum
            TempPlayer(Index).TargetType = TargetType.Npc
            SendTarget(Index, mapnpcnum, TargetType.Npc)

            If Damage > 0 Then
                PlayerAttackNpc(Index, mapnpcnum, Damage)
            Else
                PlayerMsg(Index, "Your attack does nothing.", ColorType.BrightRed)
            End If

        End If

    End Sub

    Public Function IsPlayerDead(ByVal Index As Integer)
        IsPlayerDead = False
        If Index < 0 Or Index > MAX_PLAYERS Or Not TempPlayer(Index).InGame Then Exit Function
        If GetPlayerVital(Index, Vitals.HP) <= 0 Then IsPlayerDead = True
    End Function



    Public Sub HandlePlayerKillPlayer(ByVal Attacker As Integer, ByVal Victim As Integer)
        ' Notify everyone that our player has bit the dust.
        GlobalMsg(String.Format("{0} has been killed by {1}!", GetPlayerName(Victim), GetPlayerName(Attacker)))

        ' Hand out player experience
        HandlePlayerKillExperience(Attacker, Victim)

        ' Handle our PK outcomes.
        HandlePlayerKilledPK(Attacker, Victim)

        ' Remove our player from everyone's target list.
        For Each p In TempPlayer.Where(Function(x, i) x.InGame AndAlso GetPlayerMap(i + 1) = GetPlayerMap(Victim) AndAlso x.TargetType = TargetType.Player AndAlso x.Target = Victim).Select(Function(x, i) i + 1).ToArray()
            TempPlayer(p).Target = 0
            TempPlayer(p).TargetType = TargetType.None
            SendTarget(p, 0, TargetType.None)
        Next

        ' Actually kill the player.
        OnDeath(Victim)

        ' Handle our quest system stuff.
        CheckTasks(Attacker, QuestType.Kill, 0)
    End Sub

    Public Sub HandlePlayerKillNpc(ByVal MapNum As Integer, ByVal Index As Integer, ByVal MapNpcNum As Integer)
        ' Set our attacker's target to nothing.
        SendTarget(Index, 0, TargetType.None)

        ' Hand out player experience
        HandleNpcKillExperience(Index, MapNpc(MapNum).Npc(MapNpcNum).Num)

        ' Drop items if we can.
        DropNpcItems(MapNum, MapNpcNum)

        ' Handle quest tasks related to NPC death
        CheckTasks(Index, QuestType.Slay, MapNpc(MapNum).Npc(MapNpcNum).Num)

        ' Set our NPC's data to default so we know it's dead.
        MapNpc(MapNum).Npc(MapNpcNum).Num = 0
        MapNpc(MapNum).Npc(MapNpcNum).SpawnWait = GetTimeMs()
        MapNpc(MapNum).Npc(MapNpcNum).Vital(Vitals.HP) = 0

        ' Notify all our clients that the NPC has died.
        SendNpcDead(MapNum, MapNpcNum)

        ' Check if our dead NPC is targetted by another player and remove their targets.
        For Each p In TempPlayer.Where(Function(x, i) x.InGame AndAlso GetPlayerMap(i + 1) = MapNum AndAlso x.TargetType = TargetType.Npc AndAlso x.Target = MapNpcNum).Select(Function(x, i) i + 1).ToArray()
            TempPlayer(p).Target = 0
            TempPlayer(p).TargetType = TargetType.None
            SendTarget(p, 0, TargetType.None)
        Next
    End Sub

    Public Sub HandlePlayerKilledPK(ByVal Attacker As Integer, ByVal Victim As Integer)
        ' TODO: Redo this method, it is horrendous.
        Dim z As Integer, eqcount As Integer, invcount, j As Integer
        If GetPlayerPK(Victim) = 0 Then
            If GetPlayerPK(Attacker) = 0 Then
                SetPlayerPK(Attacker, 1)
                SendPlayerData(Attacker)
                GlobalMsg(GetPlayerName(Attacker) & " has been deemed a Player Killer!!!")
            End If

        Else
            GlobalMsg(GetPlayerName(Victim) & " has paid the price for being a Player Killer!!!")
        End If

        If GetPlayerLevel(Victim) >= 10 Then

            For z = 1 To MAX_INV
                If GetPlayerInvItemNum(Victim, z) > 0 Then
                    invcount = invcount + 1
                End If
            Next

            For z = 1 To EquipmentType.Count - 1
                If GetPlayerEquipment(Victim, z) > 0 Then
                    eqcount = eqcount + 1
                End If
            Next
            z = Random(1, invcount + eqcount)

            If z = 0 Then z = 1
            If z > invcount + eqcount Then z = invcount + eqcount
            If z > invcount Then
                z = z - invcount

                For x = 1 To EquipmentType.Count - 1
                    If GetPlayerEquipment(Victim, x) > 0 Then
                        j = j + 1

                        If j = z Then
                            'Here it is, drop this piece of equipment!
                            PlayerMsg(Victim, "In death you lost grip on your " & Trim$(Item(GetPlayerEquipment(Victim, x)).Name), ColorType.BrightRed)
                            SpawnItem(GetPlayerEquipment(Victim, x), 1, GetPlayerMap(Victim), GetPlayerX(Victim), GetPlayerY(Victim))
                            SetPlayerEquipment(Victim, 0, x)
                            SendWornEquipment(Victim)
                            SendMapEquipment(Victim)
                        End If
                    End If
                Next
            Else

                For x = 1 To MAX_INV
                    If GetPlayerInvItemNum(Victim, x) > 0 Then
                        j = j + 1

                        If j = z Then
                            'Here it is, drop this item!
                            PlayerMsg(Victim, "In death you lost grip on your " & Trim$(Item(GetPlayerInvItemNum(Victim, x)).Name), ColorType.BrightRed)
                            SpawnItem(GetPlayerInvItemNum(Victim, x), GetPlayerInvItemValue(Victim, x), GetPlayerMap(Victim), GetPlayerX(Victim), GetPlayerY(Victim))
                            SetPlayerInvItemNum(Victim, x, 0)
                            SetPlayerInvItemValue(Victim, x, 0)
                            SendInventory(Victim)
                        End If
                    End If
                Next
            End If
        End If
    End Sub

#End Region

#Region "Data"
    Function GetPlayerLogin(ByVal Index As Integer) As String
        GetPlayerLogin = Trim$(Player(Index).Login)
    End Function

    Function GetPlayerIP(ByVal Index As Integer) As String
        GetPlayerIP = ""
        GetPlayerIP = GetClientIP(Index)
    End Function

    Function GetPlayerName(ByVal Index As Integer) As String
        GetPlayerName = ""
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerName = Player(Index).Character(TempPlayer(Index).CurChar).Name.Trim()
    End Function

    Sub SetPlayerAccess(ByVal Index As Integer, ByVal Access As Integer)
        Player(Index).Access = Access
    End Sub

    Sub SetPlayerSprite(ByVal Index As Integer, ByVal Sprite As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Sprite = Sprite
    End Sub

    Function GetPlayerMaxVital(ByVal Index As Integer, ByVal Vital As Vitals) As Integer

        GetPlayerMaxVital = 0

        If Index > MAX_PLAYERS Then Exit Function

        Select Case Vital
            Case Vitals.HP
                GetPlayerMaxVital = (Player(Index).Character(TempPlayer(Index).CurChar).Level + (GetPlayerStat(Index, Stats.Vitality) \ 2) + Classes(Player(Index).Character(TempPlayer(Index).CurChar).Classes).Stat(Stats.Vitality)) * 2
            Case Vitals.MP
                GetPlayerMaxVital = (Player(Index).Character(TempPlayer(Index).CurChar).Level + (GetPlayerStat(Index, Stats.Intelligence) \ 2) + Classes(Player(Index).Character(TempPlayer(Index).CurChar).Classes).Stat(Stats.Intelligence)) * 2
            Case Vitals.SP
                GetPlayerMaxVital = (Player(Index).Character(TempPlayer(Index).CurChar).Level + (GetPlayerStat(Index, Stats.Spirit) \ 2) + Classes(Player(Index).Character(TempPlayer(Index).CurChar).Classes).Stat(Stats.Spirit)) * 2
        End Select

    End Function

    Public Function GetPlayerStat(ByVal Index As Integer, ByVal Stat As Stats) As Integer
        Dim x As Integer, i As Integer

        GetPlayerStat = 0

        If Index > MAX_PLAYERS Then Exit Function

        x = Player(Index).Character(TempPlayer(Index).CurChar).Stat(Stat)

        For i = 1 To EquipmentType.Count - 1
            If Player(Index).Character(TempPlayer(Index).CurChar).Equipment(i) > 0 Then
                If Item(Player(Index).Character(TempPlayer(Index).CurChar).Equipment(i)).Add_Stat(Stat) > 0 Then
                    x = x + Item(Player(Index).Character(TempPlayer(Index).CurChar).Equipment(i)).Add_Stat(Stat)
                End If
            End If
        Next

        GetPlayerStat = x
    End Function

    Function GetPlayerAccess(ByVal Index As Integer) As Integer
        GetPlayerAccess = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerAccess = Player(Index).Access
    End Function

    Function GetPlayerMap(ByVal Index As Integer) As Integer
        GetPlayerMap = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerMap = Player(Index).Character(TempPlayer(Index).CurChar).Map
    End Function

    Function GetPlayerX(ByVal Index As Integer) As Integer
        GetPlayerX = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerX = Player(Index).Character(TempPlayer(Index).CurChar).X
    End Function

    Function GetPlayerY(ByVal Index As Integer) As Integer
        GetPlayerY = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerY = Player(Index).Character(TempPlayer(Index).CurChar).Y
    End Function

    Function GetPlayerDir(ByVal Index As Integer) As Integer
        GetPlayerDir = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerDir = Player(Index).Character(TempPlayer(Index).CurChar).Dir
    End Function

    Function GetPlayerSprite(ByVal Index As Integer) As Integer
        GetPlayerSprite = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerSprite = Player(Index).Character(TempPlayer(Index).CurChar).Sprite
    End Function

    Function GetPlayerPK(ByVal Index As Integer) As Integer
        GetPlayerPK = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerPK = Player(Index).Character(TempPlayer(Index).CurChar).Pk
    End Function

    Function GetPlayerEquipment(ByVal Index As Integer, ByVal EquipmentSlot As EquipmentType) As Byte
        GetPlayerEquipment = 0
        If Index > MAX_PLAYERS Then Exit Function
        If EquipmentSlot = 0 Then Exit Function
        GetPlayerEquipment = Player(Index).Character(TempPlayer(Index).CurChar).Equipment(EquipmentSlot)
    End Function

    Sub SetPlayerEquipment(ByVal Index As Integer, ByVal InvNum As Integer, ByVal EquipmentSlot As EquipmentType)
        Player(Index).Character(TempPlayer(Index).CurChar).Equipment(EquipmentSlot) = InvNum
    End Sub

    Sub SetPlayerDir(ByVal Index As Integer, ByVal Dir As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Dir = Dir
    End Sub

    Sub SetPlayerVital(ByVal Index As Integer, ByVal Vital As Vitals, ByVal Value As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Vital(Vital) = Value

        If GetPlayerVital(Index, Vital) > GetPlayerMaxVital(Index, Vital) Then
            Player(Index).Character(TempPlayer(Index).CurChar).Vital(Vital) = GetPlayerMaxVital(Index, Vital)
        End If

        If GetPlayerVital(Index, Vital) < 0 Then
            Player(Index).Character(TempPlayer(Index).CurChar).Vital(Vital) = 0
        End If

    End Sub

    Public Function IsDirBlocked(ByRef Blockvar As Byte, ByRef Dir As Byte) As Boolean
        If Not Blockvar And (2 ^ Dir) Then
            IsDirBlocked = False
        Else
            IsDirBlocked = True
        End If
    End Function

    Function GetPlayerVital(ByVal Index As Integer, ByVal Vital As Vitals) As Integer
        GetPlayerVital = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerVital = Player(Index).Character(TempPlayer(Index).CurChar).Vital(Vital)
    End Function

    Function GetPlayerLevel(ByVal Index As Integer) As Integer
        GetPlayerLevel = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerLevel = Player(Index).Character(TempPlayer(Index).CurChar).Level
    End Function

    Function GetPlayerPOINTS(ByVal Index As Integer) As Integer
        GetPlayerPOINTS = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerPOINTS = Player(Index).Character(TempPlayer(Index).CurChar).Points
    End Function

    Function GetPlayerNextLevel(ByVal Index As Integer) As Integer
        GetPlayerNextLevel = ((GetPlayerLevel(Index) + 1) * (GetPlayerStat(Index, Stats.Strength) + GetPlayerStat(Index, Stats.Endurance) + GetPlayerStat(Index, Stats.Intelligence) + GetPlayerStat(Index, Stats.Spirit) + GetPlayerPOINTS(Index)) + StatPtsPerLvl) * Classes(GetPlayerClass(Index)).BaseExp '25
    End Function

    Function GetPlayerExp(ByVal Index As Integer) As Integer
        GetPlayerExp = Player(Index).Character(TempPlayer(Index).CurChar).Exp
    End Function

    Sub SetPlayerMap(ByVal Index As Integer, ByVal MapNum As Integer)
        If MapNum > 0 And MapNum <= MAX_CACHED_MAPS Then
            Player(Index).Character(TempPlayer(Index).CurChar).Map = MapNum
        End If
    End Sub

    Sub SetPlayerX(ByVal Index As Integer, ByVal X As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).X = X
    End Sub

    Sub SetPlayerY(ByVal Index As Integer, ByVal Y As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Y = Y
    End Sub

    Sub SetPlayerExp(ByVal Index As Integer, ByVal Exp As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Exp = Exp
    End Sub

    Public Function GetPlayerRawStat(ByVal Index As Integer, ByVal Stat As Stats) As Integer
        GetPlayerRawStat = 0
        If Index > MAX_PLAYERS Then Exit Function

        GetPlayerRawStat = Player(Index).Character(TempPlayer(Index).CurChar).Stat(Stat)
    End Function

    Public Sub SetPlayerStat(ByVal Index As Integer, ByVal Stat As Stats, ByVal Value As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Stat(Stat) = Value
    End Sub

    Sub SetPlayerLevel(ByVal Index As Integer, ByVal Level As Integer)

        If Level > MAX_LEVELS Then Exit Sub
        Player(Index).Character(TempPlayer(Index).CurChar).Level = Level
    End Sub

    Sub SetPlayerPOINTS(ByVal Index As Integer, ByVal Points As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Points = Points
    End Sub

    Sub CheckPlayerLevelUp(ByVal Index As Integer)
        Dim expRollover As Integer
        Dim level_count As Integer

        level_count = 0

        Do While GetPlayerExp(Index) >= GetPlayerNextLevel(Index)
            expRollover = GetPlayerExp(Index) - GetPlayerNextLevel(Index)
            SetPlayerLevel(Index, GetPlayerLevel(Index) + 1)
            SetPlayerPOINTS(Index, GetPlayerPOINTS(Index) + 3)
            SetPlayerExp(Index, expRollover)
            level_count = level_count + 1
        Loop

        If level_count > 0 Then
            If level_count = 1 Then
                'singular
                GlobalMsg(GetPlayerName(Index) & " has gained " & level_count & " level!")
            Else
                'plural
                GlobalMsg(GetPlayerName(Index) & " has gained " & level_count & " levels!")
            End If
            SendExp(Index)
            SendPlayerData(Index)
        End If
    End Sub

    Function GetPlayerClass(ByVal Index As Integer) As Integer
        GetPlayerClass = Player(Index).Character(TempPlayer(Index).CurChar).Classes
    End Function

    Sub SetPlayerPK(ByVal Index As Integer, ByVal PK As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Pk = PK
    End Sub


#End Region

#Region "Incoming Packets"
    Public Sub HandleUseChar(ByVal Index As Integer)
        If Not IsPlaying(Index) Then
            JoinGame(Index)
            Dim text = String.Format("{0} | {1} has began playing {2}.", GetPlayerLogin(Index), GetPlayerName(Index), Options.GameName)
            Addlog(text, PLAYER_LOG)
            TextAdd(text)
        End If
    End Sub


#End Region

#Region "Outgoing Packets"
    Sub SendLeaveMap(ByVal Index As Integer, ByVal MapNum As Integer)
        Dim Buffer As New ByteBuffer

        Buffer.WriteInteger(ServerPackets.SLeftMap)
        Buffer.WriteInteger(Index)
        SendDataToMapBut(Index, MapNum, Buffer.ToArray())

        Buffer = Nothing
    End Sub


#End Region

#Region "Movement"
    Sub PlayerWarp(ByVal Index As Integer, ByVal MapNum As Integer, ByVal X As Integer, ByVal Y As Integer, Optional HouseTeleport As Boolean = False)
        Dim OldMap As Integer
        Dim i As Integer
        Dim Buffer As ByteBuffer

        'If (MapNum And INSTANCED_MAP_MASK) > 0 Then
        If Map(MapNum).Instanced = 1 Then
            MapNum = CreateInstance(MapNum And MAP_NUMBER_MASK)
            If MapNum = -1 Then
                'Couldn't create instanced map!
                MapNum = GetPlayerMap(Index)
                X = GetPlayerX(Index)
                Y = GetPlayerY(Index)
                AlertMsg(Index, "Unable to create a cached map!")
            Else
                'store old info, for returning to entrance of instance
                If Not TempPlayer(Index).InInstance = 1 Then
                    TempPlayer(Index).TmpMap = GetPlayerMap(Index)
                    TempPlayer(Index).TmpX = GetPlayerX(Index)
                    TempPlayer(Index).TmpY = GetPlayerY(Index)
                    TempPlayer(Index).InInstance = 1
                End If
                MapNum = MapNum + MAX_MAPS
            End If
        End If

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or MapNum <= 0 Or MapNum > MAX_CACHED_MAPS Then Exit Sub

        ' Check if you are out of bounds
        If X > Map(MapNum).MaxX Then X = Map(MapNum).MaxX
        If Y > Map(MapNum).MaxY Then Y = Map(MapNum).MaxY

        TempPlayer(Index).EventProcessingCount = 0
        TempPlayer(Index).EventMap.CurrentEvents = 0

        If HouseTeleport = False Then
            Player(Index).Character(TempPlayer(Index).CurChar).InHouse = 0
        End If

        If Player(Index).Character(TempPlayer(Index).CurChar).InHouse > 0 Then
            If IsPlaying(Player(Index).Character(TempPlayer(Index).CurChar).InHouse) Then
                If Player(Index).Character(TempPlayer(Index).CurChar).InHouse <> Player(Index).Character(TempPlayer(Index).CurChar).InHouse Then
                    Player(Index).Character(TempPlayer(Index).CurChar).InHouse = 0
                    PlayerWarp(Index, Player(Index).Character(TempPlayer(Index).CurChar).LastMap, Player(Index).Character(TempPlayer(Index).CurChar).LastX, Player(Index).Character(TempPlayer(Index).CurChar).LastY)
                    Exit Sub
                Else
                    SendFurnitureToHouse(Player(Index).Character(TempPlayer(Index).CurChar).InHouse)
                End If
            End If
        End If

        'clear target
        TempPlayer(Index).Target = 0
        TempPlayer(Index).TargetType = TargetType.None
        SendTarget(Index, 0, TargetType.None)

        ' Save old map to send erase player data to
        OldMap = GetPlayerMap(Index)

        If OldMap <> MapNum Then
            SendLeaveMap(Index, OldMap)
        End If

        SetPlayerMap(Index, MapNum)
        SetPlayerX(Index, X)
        SetPlayerY(Index, Y)
        If PetAlive(Index) Then
            SetPetX(Index, X)
            SetPetY(Index, Y)
            TempPlayer(Index).PetTarget = 0
            TempPlayer(Index).PetTargetType = 0
            SendPetXY(Index, X, Y)
        End If

        SendPlayerXY(Index)

        ' send equipment of all people on new map
        If GetTotalMapPlayers(MapNum) > 0 Then
            For i = 1 To GetPlayersOnline()
                If IsPlaying(i) Then
                    If GetPlayerMap(i) = MapNum Then
                        SendMapEquipmentTo(i, Index)
                    End If
                End If
            Next
        End If

        ' Now we check if there were any players left on the map the player just left, and if not stop processing npcs
        If GetTotalMapPlayers(OldMap) = 0 Then
            PlayersOnMap(OldMap) = False

            If IsInstancedMap(OldMap) Then
                DestroyInstancedMap(OldMap - MAX_MAPS)
            End If

            ' Regenerate all NPCs' health
            For i = 1 To MAX_MAP_NPCS

                If MapNpc(OldMap).Npc(i).Num > 0 Then
                    MapNpc(OldMap).Npc(i).Vital(Vitals.HP) = GetNpcMaxVital(MapNpc(OldMap).Npc(i).Num, Vitals.HP)
                End If

            Next

        End If

        ' Sets it so we know to process npcs on the map
        PlayersOnMap(MapNum) = True
        TempPlayer(Index).GettingMap = True

        CheckTasks(Index, QuestType.Reach, MapNum)

        Buffer = New ByteBuffer
        Buffer.WriteInteger(ServerPackets.SCheckForMap)
        Buffer.WriteInteger(MapNum)
        Buffer.WriteInteger(Map(MapNum).Revision)
        SendDataTo(Index, Buffer.ToArray())

        Buffer = Nothing

    End Sub

    Sub PlayerMove(ByVal Index As Integer, ByVal Dir As Integer, ByVal Movement As Integer, ExpectingWarp As Boolean)
        Dim MapNum As Integer, Buffer As ByteBuffer
        Dim x As Integer, y As Integer, begineventprocessing As Boolean
        Dim Moved As Boolean, DidWarp As Boolean
        Dim NewMapX As Byte, NewMapY As Byte
        Dim VitalType As Integer, Colour As Integer, amount As Integer

        'Debug.Print("Server-PlayerMove")

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or Dir < Direction.Up Or Dir > Direction.Right Or Movement < 1 Or Movement > 2 Then
            Exit Sub
        End If

        SetPlayerDir(Index, Dir)
        Moved = False
        MapNum = GetPlayerMap(Index)

        Select Case Dir
            Case Direction.Up

                ' Check to make sure not outside of boundries
                If GetPlayerY(Index) > 0 Then

                    ' Check to make sure that the tile is walkable
                    If Not IsDirBlocked(Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index)).DirBlock, Direction.Up + 1) Then
                        If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) - 1).Type <> TileType.Blocked Then
                            If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) - 1).Type <> TileType.Resource Then

                                ' Check to see if the tile is a key and if it is check if its opened
                                If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) - 1).Type <> TileType.Key Or (Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) - 1).Type = TileType.Key And TempTile(GetPlayerMap(Index)).DoorOpen(GetPlayerX(Index), GetPlayerY(Index) - 1) = True) Then
                                    SetPlayerY(Index, GetPlayerY(Index) - 1)
                                    SendPlayerMove(Index, Movement)
                                    Moved = True
                                End If

                                'check for event
                                For i = 1 To TempPlayer(Index).EventMap.CurrentEvents
                                    If TempPlayer(Index).EventMap.EventPages(i).X = GetPlayerX(Index) And TempPlayer(Index).EventMap.EventPages(i).Y = GetPlayerY(Index) - 1 Then
                                        If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).Trigger = 1 Then
                                            'PlayerMsg(Index, "OnTouch event", ColorType.Red)
                                            'Process this event, it is on-touch and everything checks out.
                                            If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount > 0 Then

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurList = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurSlot = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).EventID = TempPlayer(Index).EventMap.EventPages(i).EventID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).PageID = TempPlayer(Index).EventMap.EventPages(i).PageID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).WaitingForResponse = 0
                                                ReDim TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ListLeftOff(0 To Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount)

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).Active = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ActionTimer = GetTimeMs()
                                            End If
                                        End If
                                    End If
                                Next

                            End If
                        End If
                    End If

                Else

                    ' Check to see if we can move them to the another map
                    If Map(GetPlayerMap(Index)).Up > 0 Then
                        NewMapY = Map(Map(GetPlayerMap(Index)).Up).MaxY
                        PlayerWarp(Index, Map(GetPlayerMap(Index)).Up, GetPlayerX(Index), NewMapY)
                        DidWarp = True
                        Moved = True
                    End If
                End If

            Case Direction.Down

                ' Check to make sure not outside of boundries
                If GetPlayerY(Index) < Map(MapNum).MaxY Then

                    ' Check to make sure that the tile is walkable
                    If Not IsDirBlocked(Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index)).DirBlock, Direction.Down + 1) Then
                        If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) + 1).Type <> TileType.Blocked Then
                            If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) + 1).Type <> TileType.Resource Then

                                ' Check to see if the tile is a key and if it is check if its opened
                                If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) + 1).Type <> TileType.Key Or (Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index) + 1).Type = TileType.Key And TempTile(GetPlayerMap(Index)).DoorOpen(GetPlayerX(Index), GetPlayerY(Index) + 1) = True) Then
                                    SetPlayerY(Index, GetPlayerY(Index) + 1)
                                    SendPlayerMove(Index, Movement)
                                    Moved = True
                                End If

                                'check for event
                                For i = 1 To TempPlayer(Index).EventMap.CurrentEvents
                                    If TempPlayer(Index).EventMap.EventPages(i).X = GetPlayerX(Index) And TempPlayer(Index).EventMap.EventPages(i).Y = GetPlayerY(Index) + 1 Then
                                        If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).Trigger = 1 Then
                                            'PlayerMsg(Index, "OnTouch event", ColorType.Red)
                                            'Process this event, it is on-touch and everything checks out.
                                            If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount > 0 Then

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurList = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurSlot = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).EventID = TempPlayer(Index).EventMap.EventPages(i).EventID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).PageID = TempPlayer(Index).EventMap.EventPages(i).PageID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).WaitingForResponse = 0
                                                ReDim TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ListLeftOff(0 To Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount)

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).Active = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ActionTimer = GetTimeMs()
                                            End If
                                        End If
                                    End If
                                Next
                            End If
                        End If
                    End If

                Else

                    ' Check to see if we can move them to the another map
                    If Map(GetPlayerMap(Index)).Down > 0 Then
                        PlayerWarp(Index, Map(GetPlayerMap(Index)).Down, GetPlayerX(Index), 0)
                        DidWarp = True
                        Moved = True
                    End If
                End If

            Case Direction.Left

                ' Check to make sure not outside of boundries
                If GetPlayerX(Index) > 0 Then

                    ' Check to make sure that the tile is walkable
                    If Not IsDirBlocked(Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index)).DirBlock, Direction.Left + 1) Then
                        If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) - 1, GetPlayerY(Index)).Type <> TileType.Blocked Then
                            If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) - 1, GetPlayerY(Index)).Type <> TileType.Resource Then

                                ' Check to see if the tile is a key and if it is check if its opened
                                If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) - 1, GetPlayerY(Index)).Type <> TileType.Key Or (Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) - 1, GetPlayerY(Index)).Type = TileType.Key And TempTile(GetPlayerMap(Index)).DoorOpen(GetPlayerX(Index) - 1, GetPlayerY(Index)) = True) Then
                                    SetPlayerX(Index, GetPlayerX(Index) - 1)
                                    SendPlayerMove(Index, Movement)
                                    Moved = True
                                End If

                                'check for event
                                For i = 1 To TempPlayer(Index).EventMap.CurrentEvents
                                    If TempPlayer(Index).EventMap.EventPages(i).X = GetPlayerX(Index) - 1 And TempPlayer(Index).EventMap.EventPages(i).Y = GetPlayerY(Index) Then
                                        If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).Trigger = 1 Then
                                            'PlayerMsg(Index, "OnTouch event", ColorType.Red)
                                            'Process this event, it is on-touch and everything checks out.
                                            If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount > 0 Then

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurList = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurSlot = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).EventID = TempPlayer(Index).EventMap.EventPages(i).EventID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).PageID = TempPlayer(Index).EventMap.EventPages(i).PageID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).WaitingForResponse = 0
                                                ReDim TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ListLeftOff(0 To Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount)

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).Active = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ActionTimer = GetTimeMs()
                                            End If
                                        End If
                                    End If
                                Next
                            End If
                        End If
                    End If

                Else

                    ' Check to see if we can move them to the another map
                    If Map(GetPlayerMap(Index)).Left > 0 Then
                        NewMapX = Map(Map(GetPlayerMap(Index)).Left).MaxX
                        PlayerWarp(Index, Map(GetPlayerMap(Index)).Left, NewMapX, GetPlayerY(Index))
                        DidWarp = True
                        Moved = True
                    End If
                End If

            Case Direction.Right

                ' Check to make sure not outside of boundries
                If GetPlayerX(Index) < Map(MapNum).MaxX Then

                    ' Check to make sure that the tile is walkable
                    If Not IsDirBlocked(Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index)).DirBlock, Direction.Right + 1) Then
                        If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) + 1, GetPlayerY(Index)).Type <> TileType.Blocked Then
                            If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) + 1, GetPlayerY(Index)).Type <> TileType.Resource Then

                                ' Check to see if the tile is a key and if it is check if its opened
                                If Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) + 1, GetPlayerY(Index)).Type <> TileType.Key Or (Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index) + 1, GetPlayerY(Index)).Type = TileType.Key And TempTile(GetPlayerMap(Index)).DoorOpen(GetPlayerX(Index) + 1, GetPlayerY(Index)) = True) Then
                                    SetPlayerX(Index, GetPlayerX(Index) + 1)
                                    SendPlayerMove(Index, Movement)
                                    Moved = True
                                End If

                                'check for event
                                For i = 1 To TempPlayer(Index).EventMap.CurrentEvents
                                    If TempPlayer(Index).EventMap.EventPages(i).X = GetPlayerX(Index) And TempPlayer(Index).EventMap.EventPages(i).Y = GetPlayerY(Index) Then
                                        If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).Trigger = 1 Then
                                            'PlayerMsg(Index, "OnTouch event", ColorType.Red)
                                            'Process this event, it is on-touch and everything checks out.
                                            If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount > 0 Then

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurList = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurSlot = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).EventID = TempPlayer(Index).EventMap.EventPages(i).EventID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).PageID = TempPlayer(Index).EventMap.EventPages(i).PageID
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).WaitingForResponse = 0
                                                ReDim TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ListLeftOff(0 To Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount)

                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).Active = 1
                                                TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ActionTimer = GetTimeMs()
                                            End If
                                        End If
                                    End If
                                Next
                            End If
                        End If
                    End If

                Else

                    ' Check to see if we can move them to the another map
                    If Map(GetPlayerMap(Index)).Right > 0 Then
                        PlayerWarp(Index, Map(GetPlayerMap(Index)).Right, 0, GetPlayerY(Index))
                        DidWarp = True
                        Moved = True
                    End If
                End If
        End Select

        With Map(GetPlayerMap(Index)).Tile(GetPlayerX(Index), GetPlayerY(Index))
            ' Check to see if the tile is a warp tile, and if so warp them
            If .Type = TileType.Warp Then
                MapNum = .Data1
                x = .Data2
                y = .Data3

                'If (MapNum And INSTANCED_MAP_MASK) > 0 Then
                If Map(MapNum).Instanced = 1 Then
                    If TempPlayer(Index).InParty Then
                        PartyWarp(Index, MapNum, x, y)
                    Else
                        PlayerWarp(Index, MapNum, x, y)
                    End If
                Else
                    PlayerWarp(Index, MapNum, x, y)
                End If

                DidWarp = True
                Moved = True
            End If

            ' Check to see if the tile is a door tile, and if so warp them
            If .Type = TileType.Door Then
                MapNum = .Data1
                x = .Data2
                y = .Data3
                ' send the animation to the map
                SendDoorAnimation(GetPlayerMap(Index), GetPlayerX(Index), GetPlayerY(Index))

                If Map(MapNum).Instanced = 1 Then
                    If TempPlayer(Index).InParty Then
                        PartyWarp(Index, MapNum, x, y)
                    Else
                        PlayerWarp(Index, MapNum, x, y)
                    End If
                Else
                    PlayerWarp(Index, MapNum, x, y)
                End If
                DidWarp = True
                Moved = True
            End If

            ' Check for key trigger open
            If .Type = TileType.KeyOpen Then
                x = .Data1
                y = .Data2

                If Map(GetPlayerMap(Index)).Tile(x, y).Type = TileType.Key And TempTile(GetPlayerMap(Index)).DoorOpen(x, y) = False Then
                    TempTile(GetPlayerMap(Index)).DoorOpen(x, y) = True
                    TempTile(GetPlayerMap(Index)).DoorTimer = GetTimeMs()
                    SendMapKey(Index, x, y, 1)
                    MapMsg(GetPlayerMap(Index), "A door has been unlocked.", ColorType.White)
                End If
            End If

            ' Check for a shop, and if so open it
            If .Type = TileType.Shop Then
                x = .Data1
                If x > 0 Then ' shop exists?
                    If Len(Trim$(Shop(x).Name)) > 0 Then ' name exists?
                        SendOpenShop(Index, x)
                        TempPlayer(Index).InShop = x ' stops movement and the like
                    End If
                End If
            End If

            ' Check to see if the tile is a bank, and if so send bank
            If .Type = TileType.Bank Then
                SendBank(Index)
                TempPlayer(Index).InBank = True
                Moved = True
            End If

            ' Check if it's a heal tile
            If .Type = TileType.Heal Then
                VitalType = .Data1
                amount = .Data2
                If Not GetPlayerVital(Index, VitalType) = GetPlayerMaxVital(Index, VitalType) Then
                    If VitalType = Enums.Vitals.HP Then
                        Colour = ColorType.BrightGreen
                    Else
                        Colour = ColorType.BrightBlue
                    End If
                    SendActionMsg(GetPlayerMap(Index), "+" & amount, Colour, ActionMsgType.Scroll, GetPlayerX(Index) * 32, GetPlayerY(Index) * 32, 1)
                    SetPlayerVital(Index, VitalType, GetPlayerVital(Index, VitalType) + amount)
                    PlayerMsg(Index, "You feel rejuvinating forces coarsing through your body.", ColorType.BrightGreen)
                    SendVital(Index, VitalType)
                    ' send vitals to party if in one
                    If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)
                End If
                Moved = True
            End If

            ' Check if it's a trap tile
            If .Type = TileType.Trap Then
                amount = .Data1
                SendActionMsg(GetPlayerMap(Index), "-" & amount, ColorType.BrightRed, ActionMsgType.Scroll, GetPlayerX(Index) * 32, GetPlayerY(Index) * 32, 1)
                If GetPlayerVital(Index, Enums.Vitals.HP) - amount <= 0 Then
                    KillPlayer(Index)
                    PlayerMsg(Index, "You've been killed by a trap.", ColorType.BrightRed)
                Else
                    SetPlayerVital(Index, Enums.Vitals.HP, GetPlayerVital(Index, Enums.Vitals.HP) - amount)
                    PlayerMsg(Index, "You've been injured by a trap.", ColorType.BrightRed)
                    SendVital(Index, Enums.Vitals.HP)
                    ' send vitals to party if in one
                    If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)
                End If
                Moved = True
            End If

            'Housing
            If .Type = TileType.House Then
                If Player(Index).Character(TempPlayer(Index).CurChar).House.HouseIndex = .Data1 Then
                    'Do warping and such to the player's house :/
                    Player(Index).Character(TempPlayer(Index).CurChar).LastMap = GetPlayerMap(Index)
                    Player(Index).Character(TempPlayer(Index).CurChar).LastX = GetPlayerX(Index)
                    Player(Index).Character(TempPlayer(Index).CurChar).LastY = GetPlayerY(Index)
                    Player(Index).Character(TempPlayer(Index).CurChar).InHouse = Index
                    SendDataTo(Index, PlayerData(Index))
                    PlayerWarp(Index, HouseConfig(Player(Index).Character(TempPlayer(Index).CurChar).House.HouseIndex).BaseMap, HouseConfig(Player(Index).Character(TempPlayer(Index).CurChar).House.HouseIndex).X, HouseConfig(Player(Index).Character(TempPlayer(Index).CurChar).House.HouseIndex).Y, True)
                    DidWarp = True
                    Exit Sub
                Else
                    'Send the buy sequence and see what happens. (To be recreated in events.)
                    Buffer = New ByteBuffer
                    Buffer.WriteInteger(ServerPackets.SBuyHouse)
                    Buffer.WriteInteger(.Data1)
                    SendDataTo(Index, Buffer.ToArray)
                    Buffer = Nothing
                    TempPlayer(Index).BuyHouseIndex = .Data1
                End If
            End If

            'crafting
            If .Type = TileType.Craft Then
                TempPlayer(Index).IsCrafting = True
                SendPlayerRecipes(Index)
                SendOpenCraft(Index)
                Moved = True
            End If

        End With

        If Moved = True Then
            If Player(Index).Character(TempPlayer(Index).CurChar).InHouse > 0 Then
                If Player(Index).Character(TempPlayer(Index).CurChar).X = HouseConfig(Player(Player(Index).Character(TempPlayer(Index).CurChar).InHouse).Character(TempPlayer(Index).CurChar).House.HouseIndex).X Then
                    If Player(Index).Character(TempPlayer(Index).CurChar).Y = HouseConfig(Player(Player(Index).Character(TempPlayer(Index).CurChar).InHouse).Character(TempPlayer(Index).CurChar).House.HouseIndex).Y Then
                        PlayerWarp(Index, Player(Index).Character(TempPlayer(Index).CurChar).LastMap, Player(Index).Character(TempPlayer(Index).CurChar).LastX, Player(Index).Character(TempPlayer(Index).CurChar).LastY)
                        DidWarp = True
                    End If
                End If
            End If
        End If

        ' They tried to hack
        If Moved = False Or (ExpectingWarp And Not DidWarp) Then
            PlayerWarp(Index, GetPlayerMap(Index), GetPlayerX(Index), GetPlayerY(Index))
        End If

        x = GetPlayerX(Index)
        y = GetPlayerY(Index)

        If Moved = True Then
            If TempPlayer(Index).EventMap.CurrentEvents > 0 Then
                For i = 1 To TempPlayer(Index).EventMap.CurrentEvents
                    If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Globals = 1 Then
                        If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).X = x And Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Y = y And Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).Trigger = 1 And TempPlayer(Index).EventMap.EventPages(i).Visible = 1 Then begineventprocessing = True
                    Else
                        If TempPlayer(Index).EventMap.EventPages(i).X = x And TempPlayer(Index).EventMap.EventPages(i).Y = y And Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).Trigger = 1 And TempPlayer(Index).EventMap.EventPages(i).Visible = 1 Then begineventprocessing = True
                    End If
                    begineventprocessing = False
                    If begineventprocessing = True Then
                        'Process this event, it is on-touch and everything checks out.
                        If Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount > 0 Then
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).Active = 1
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ActionTimer = GetTimeMs()
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurList = 1
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).CurSlot = 1
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).EventID = TempPlayer(Index).EventMap.EventPages(i).EventID
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).PageID = TempPlayer(Index).EventMap.EventPages(i).PageID
                            TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).WaitingForResponse = 0
                            ReDim TempPlayer(Index).EventProcessing(TempPlayer(Index).EventMap.EventPages(i).EventID).ListLeftOff(0 To Map(GetPlayerMap(Index)).Events(TempPlayer(Index).EventMap.EventPages(i).EventID).Pages(TempPlayer(Index).EventMap.EventPages(i).PageID).CommandListCount)
                        End If
                        begineventprocessing = False
                    End If
                Next
            End If
        End If

    End Sub

#End Region

#Region "Inventory"

    Function HasItem(ByVal Index As Integer, ByVal ItemNum As Integer) As Integer
        Dim i As Integer

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or ItemNum <= 0 Or ItemNum > MAX_ITEMS Then
            Exit Function
        End If

        For i = 1 To MAX_INV
            ' Check to see if the player has the item
            If GetPlayerInvItemNum(Index, i) = ItemNum Then
                If Item(ItemNum).Type = ItemType.Currency Or Item(ItemNum).Stackable = 1 Then
                    HasItem = GetPlayerInvItemValue(Index, i)
                Else
                    HasItem = 1
                End If
                Exit Function
            End If
        Next

    End Function

    Function FindItemSlot(ByVal Index As Integer, ByVal ItemNum As Integer) As Integer
        Dim i As Integer

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or ItemNum <= 0 Or ItemNum > MAX_ITEMS Then
            Exit Function
        End If

        For i = 1 To MAX_INV
            ' Check to see if the player has the item
            If GetPlayerInvItemNum(Index, i) = ItemNum Then
                FindItemSlot = i
                Exit Function
            End If
        Next

    End Function

    Function GetPlayerInvItemNum(ByVal Index As Integer, ByVal InvSlot As Integer) As Integer
        GetPlayerInvItemNum = 0
        If Index > MAX_PLAYERS Then Exit Function
        If InvSlot = 0 Then Exit Function

        GetPlayerInvItemNum = Player(Index).Character(TempPlayer(Index).CurChar).Inv(InvSlot).Num
    End Function

    Function GetPlayerInvItemValue(ByVal Index As Integer, ByVal InvSlot As Integer) As Integer
        GetPlayerInvItemValue = 0
        If Index > MAX_PLAYERS Then Exit Function
        GetPlayerInvItemValue = Player(Index).Character(TempPlayer(Index).CurChar).Inv(InvSlot).Value
    End Function

    Sub PlayerMapGetItem(ByVal Index As Integer)
        Dim i As Integer, itemnum As Integer
        Dim n As Integer
        Dim MapNum As Integer
        Dim Msg As String

        If Not IsPlaying(Index) Then Exit Sub
        MapNum = GetPlayerMap(Index)

        For i = 1 To MAX_MAP_ITEMS

            ' See if theres even an item here
            If (MapItem(MapNum, i).Num > 0) Then
                If (MapItem(MapNum, i).Num <= MAX_ITEMS) Then

                    ' Check if item is at the same location as the player
                    If (MapItem(MapNum, i).X = GetPlayerX(Index)) Then
                        If (MapItem(MapNum, i).Y = GetPlayerY(Index)) Then
                            ' Find open slot
                            n = FindOpenInvSlot(Index, MapItem(MapNum, i).Num)

                            ' Open slot available?
                            If n <> 0 Then
                                ' Set item in players inventor
                                itemnum = MapItem(MapNum, i).Num

                                If Item(itemnum).Randomize <> 0 Then
                                    If Trim(MapItem(MapNum, i).RandData.Prefix) <> "" Or Trim(MapItem(MapNum, i).RandData.Suffix) <> "" Then
                                        Player(Index).Character(TempPlayer(Index).CurChar).RandInv(n).Prefix = MapItem(MapNum, i).RandData.Prefix
                                        Player(Index).Character(TempPlayer(Index).CurChar).RandInv(n).Suffix = MapItem(MapNum, i).RandData.Suffix
                                        Player(Index).Character(TempPlayer(Index).CurChar).RandInv(n).Rarity = MapItem(MapNum, i).RandData.Rarity
                                        Player(Index).Character(TempPlayer(Index).CurChar).RandInv(n).Damage = MapItem(MapNum, i).RandData.Damage
                                        Player(Index).Character(TempPlayer(Index).CurChar).RandInv(n).Speed = MapItem(MapNum, i).RandData.Speed
                                        For m = 1 To Stats.Count - 1
                                            Player(Index).Character(TempPlayer(Index).CurChar).RandInv(n).Stat(m) = MapItem(GetPlayerMap(Index), i).RandData.Stat(m)
                                        Next m
                                    Else ' Nothing has been generated yet!
                                        GivePlayerRandomItem(Index, itemnum, n)
                                    End If
                                End If

                                SetPlayerInvItemNum(Index, n, MapItem(MapNum, i).Num)

                                If Item(GetPlayerInvItemNum(Index, n)).Type = ItemType.Currency Or Item(GetPlayerInvItemNum(Index, n)).Stackable = 1 Then
                                    SetPlayerInvItemValue(Index, n, GetPlayerInvItemValue(Index, n) + MapItem(MapNum, i).Value)
                                    Msg = MapItem(MapNum, i).Value & " " & Trim$(Item(GetPlayerInvItemNum(Index, n)).Name)
                                Else
                                    SetPlayerInvItemValue(Index, n, 0)
                                    Msg = CheckGrammar(Trim$(Item(GetPlayerInvItemNum(Index, n)).Name), 1)
                                End If

                                ' Erase item from the map
                                MapItem(MapNum, i).Num = 0
                                MapItem(MapNum, i).Value = 0
                                MapItem(MapNum, i).X = 0
                                MapItem(MapNum, i).Y = 0

                                SendInventoryUpdate(Index, n)
                                SpawnItemSlot(i, 0, 0, GetPlayerMap(Index), 0, 0)

                                SendActionMsg(GetPlayerMap(Index), Msg, ColorType.White, 1, (GetPlayerX(Index) * 32), (GetPlayerY(Index) * 32))
                                CheckTasks(Index, QuestType.Gather, GetItemNum(Trim$(Item(GetPlayerInvItemNum(Index, n)).Name)))
                                Exit For
                            Else
                                PlayerMsg(Index, "Your inventory is full.", ColorType.BrightRed)
                                Exit For
                            End If
                        End If
                    End If
                End If
            End If
        Next
    End Sub

    Sub SetPlayerInvItemValue(ByVal Index As Integer, ByVal InvSlot As Integer, ByVal ItemValue As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Inv(InvSlot).Value = ItemValue
    End Sub

    Sub SetPlayerInvItemNum(ByVal Index As Integer, ByVal invSlot As Integer, ByVal itemNum As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Inv(invSlot).Num = itemNum
    End Sub

    Function FindOpenInvSlot(ByVal Index As Integer, ByVal ItemNum As Integer) As Integer
        Dim i As Integer

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or ItemNum <= 0 Or ItemNum > MAX_ITEMS Then
            Exit Function
        End If

        If Item(ItemNum).Type = ItemType.Currency Or Item(ItemNum).Stackable = 1 Then
            ' If currency then check to see if they already have an instance of the item and add it to that
            For i = 1 To MAX_INV
                If GetPlayerInvItemNum(Index, i) = ItemNum Then
                    FindOpenInvSlot = i
                    Exit Function
                End If
            Next
        End If

        For i = 1 To MAX_INV
            ' Try to find an open free slot
            If GetPlayerInvItemNum(Index, i) = 0 Then
                FindOpenInvSlot = i
                Exit Function
            End If
        Next

    End Function

    Function TakeInvItem(ByVal Index As Integer, ByVal ItemNum As Integer, ByVal ItemVal As Integer) As Boolean
        Dim i As Integer

        TakeInvItem = False

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or ItemNum <= 0 Or ItemNum > MAX_ITEMS Then
            Exit Function
        End If

        For i = 1 To MAX_INV

            ' Check to see if the player has the item
            If GetPlayerInvItemNum(Index, i) = ItemNum Then
                If Item(ItemNum).Type = ItemType.Currency Or Item(ItemNum).Stackable = 1 Then

                    ' Is what we are trying to take away more then what they have?  If so just set it to zero
                    If ItemVal >= GetPlayerInvItemValue(Index, i) Then
                        TakeInvItem = True
                    Else
                        SetPlayerInvItemValue(Index, i, GetPlayerInvItemValue(Index, i) - ItemVal)
                        SendInventoryUpdate(Index, i)
                    End If
                Else
                    TakeInvItem = True
                End If

                If TakeInvItem Then
                    SetPlayerInvItemNum(Index, i, 0)
                    SetPlayerInvItemValue(Index, i, 0)
                    ' Send the inventory update
                    SendInventoryUpdate(Index, i)
                    Exit Function
                End If
            End If

        Next

    End Function

    Function GiveInvItem(ByVal Index As Integer, ByVal ItemNum As Integer, ByVal ItemVal As Integer, Optional ByVal SendUpdate As Boolean = True) As Boolean
        Dim i As Integer

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or ItemNum <= 0 Or ItemNum > MAX_ITEMS Then
            GiveInvItem = False
            Exit Function
        End If

        i = FindOpenInvSlot(Index, ItemNum)

        ' Check to see if inventory is full
        If i <> 0 Then
            SetPlayerInvItemNum(Index, i, ItemNum)
            SetPlayerInvItemValue(Index, i, GetPlayerInvItemValue(Index, i) + ItemVal)
            If SendUpdate Then SendInventoryUpdate(Index, i)
            GiveInvItem = True
        Else
            PlayerMsg(Index, "Your inventory is full.", ColorType.BrightRed)
            GiveInvItem = False
        End If

    End Function

    Sub PlayerMapDropItem(ByVal Index As Integer, ByVal InvNum As Integer, ByVal Amount As Integer)
        Dim i As Integer

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or InvNum <= 0 Or InvNum > MAX_INV Then
            Exit Sub
        End If

        ' check the player isn't doing something
        If TempPlayer(Index).InBank Or TempPlayer(Index).InShop Or TempPlayer(Index).InTrade > 0 Then Exit Sub

        If (GetPlayerInvItemNum(Index, InvNum) > 0) Then
            If (GetPlayerInvItemNum(Index, InvNum) <= MAX_ITEMS) Then
                i = FindOpenMapItemSlot(GetPlayerMap(Index))

                If i <> 0 Then
                    MapItem(GetPlayerMap(Index), i).Num = GetPlayerInvItemNum(Index, InvNum)
                    MapItem(GetPlayerMap(Index), i).X = GetPlayerX(Index)
                    MapItem(GetPlayerMap(Index), i).Y = GetPlayerY(Index)

                    If Item(GetPlayerInvItemNum(Index, InvNum)).Type = ItemType.Currency Or Item(GetPlayerInvItemNum(Index, InvNum)).Stackable = 1 Then

                        ' Check if its more then they have and if so drop it all
                        If Amount >= GetPlayerInvItemValue(Index, InvNum) Then
                            MapItem(GetPlayerMap(Index), i).Value = GetPlayerInvItemValue(Index, InvNum)
                            SetPlayerInvItemNum(Index, InvNum, 0)
                            SetPlayerInvItemValue(Index, InvNum, 0)
                            Amount = GetPlayerInvItemValue(Index, InvNum)
                        Else
                            MapItem(GetPlayerMap(Index), i).Value = Amount
                            SetPlayerInvItemValue(Index, InvNum, GetPlayerInvItemValue(Index, InvNum) - Amount)
                        End If
                        Call MapMsg(GetPlayerMap(Index), String.Format("{0} has dropped {1} ({2}x).", GetPlayerName(Index), CheckGrammar(Trim$(Item(GetPlayerInvItemNum(Index, InvNum)).Name)), Amount), ColorType.Yellow)
                    Else
                        ' Its not a currency object so this is easy
                        MapItem(GetPlayerMap(Index), i).Value = 0
                        ' send message

                        MapMsg(GetPlayerMap(Index), String.Format("{0} has dropped {1}.", GetPlayerName(Index), CheckGrammar(Trim$(Item(GetPlayerInvItemNum(Index, InvNum)).Name))), ColorType.Yellow)
                        SetPlayerInvItemNum(Index, InvNum, 0)
                        SetPlayerInvItemValue(Index, InvNum, 0)
                    End If

                    ' Send inventory update
                    SendInventoryUpdate(Index, InvNum)
                    ' Spawn the item before we set the num or we'll get a different free map item slot
                    SpawnItemSlot(i, MapItem(GetPlayerMap(Index), i).Num, Amount, GetPlayerMap(Index), GetPlayerX(Index), GetPlayerY(Index))
                Else
                    PlayerMsg(Index, "Too many items already on the ground.", ColorType.Yellow)
                End If
            End If
        End If

    End Sub

    Function TakeInvSlot(ByVal Index As Integer, ByVal InvSlot As Integer, ByVal ItemVal As Integer) As Boolean
        Dim itemNum

        TakeInvSlot = False

        ' Check for subscript out of range
        If IsPlaying(Index) = False Or InvSlot <= 0 Or InvSlot > MAX_ITEMS Then Exit Function

        itemNum = GetPlayerInvItemNum(Index, InvSlot)

        If Item(itemNum).Type = ItemType.Currency Or Item(itemNum).Stackable = 1 Then

            ' Is what we are trying to take away more then what they have?  If so just set it to zero
            If ItemVal >= GetPlayerInvItemValue(Index, InvSlot) Then
                TakeInvSlot = True
            Else
                SetPlayerInvItemValue(Index, InvSlot, GetPlayerInvItemValue(Index, InvSlot) - ItemVal)
            End If
        Else
            TakeInvSlot = True
        End If

        If TakeInvSlot Then
            SetPlayerInvItemNum(Index, InvSlot, 0)
            SetPlayerInvItemValue(Index, InvSlot, 0)
            Exit Function
        End If

    End Function

    Public Sub UseItem(ByVal Index As Integer, ByVal InvNum As Integer)
        Dim InvItemNum As Integer, i As Integer, n As Integer, x As Integer, y As Integer, tempitem As Integer
        Dim m As Integer, tempdata(Stats.Count + 3) As Long, tempstr(2) As String

        ' Prevent hacking
        If InvNum < 1 Or InvNum > MAX_ITEMS Then Exit Sub

        If (GetPlayerInvItemNum(Index, InvNum) > 0) And (GetPlayerInvItemNum(Index, InvNum) <= MAX_ITEMS) Then
            InvItemNum = GetPlayerInvItemNum(Index, InvNum)

            n = Item(InvItemNum).Data2

            ' Find out what kind of item it is
            Select Case Item(InvItemNum).Type
                Case ItemType.Equipment
                    For i = 1 To Stats.Count - 1
                        If GetPlayerStat(Index, i) < Item(InvItemNum).Stat_Req(i) Then
                            PlayerMsg(Index, "You do not meet the stat requirements to equip this item.", ColorType.BrightRed)
                            Exit Sub
                        End If
                    Next

                    ' Make sure they are the right level
                    i = Item(InvItemNum).LevelReq

                    If i > GetPlayerLevel(Index) Then
                        PlayerMsg(Index, "You do not meet the level requirements to equip this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    ' Make sure they are the right class
                    If Not Item(InvItemNum).ClassReq = GetPlayerClass(Index) And Not Item(InvItemNum).ClassReq = 0 Then
                        PlayerMsg(Index, "You do not meet the class requirements to equip this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    ' access requirement
                    If Not GetPlayerAccess(Index) >= Item(InvItemNum).AccessReq Then
                        PlayerMsg(Index, "You do not meet the access requirement to equip this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    'if that went fine, we progress the subtype

                    Select Case Item(InvItemNum).SubType
                        Case EquipmentType.Weapon

                            If Item(InvItemNum).TwoHanded > 0 Then
                                If GetPlayerEquipment(Index, EquipmentType.Shield) > 0 Then
                                    PlayerMsg(Index, "This is a 2Handed weapon! Please unequip shield first.", ColorType.BrightRed)
                                    Exit Sub
                                End If
                            End If

                            If GetPlayerEquipment(Index, EquipmentType.Weapon) > 0 Then
                                tempitem = GetPlayerEquipment(Index, EquipmentType.Weapon)
                                tempstr(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Prefix
                                tempstr(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Suffix
                                tempdata(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Damage
                                tempdata(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Speed
                                tempdata(3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Rarity
                                For i = 1 To Stats.Count - 1
                                    tempdata(i + 3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Stat(i)
                                Next
                            End If

                            SetPlayerEquipment(Index, InvItemNum, EquipmentType.Weapon)

                            ' Transfer the Inventory data to the Equipment data
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Prefix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Suffix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Damage
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Speed
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Rarity

                            For i = 1 To Stats.Count - 1
                                Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Weapon).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Stat(i)
                            Next

                            If Item(InvItemNum).Randomize <> 0 Then
                                PlayerMsg(Index, "You equip " & tempstr(1) & " " & CheckGrammar(Item(InvItemNum).Name) & " " & tempstr(2), ColorType.BrightGreen)
                            Else
                                PlayerMsg(Index, "You equip " & CheckGrammar(Item(InvItemNum).Name), ColorType.BrightGreen)
                            End If

                            SetPlayerInvItemNum(Index, InvNum, 0)
                            SetPlayerInvItemValue(Index, InvNum, 0)
                            ClearRandInv(Index, InvNum)

                            If tempitem > 0 Then ' give back the stored item
                                m = FindOpenInvSlot(Index, tempitem)
                                SetPlayerInvItemNum(Index, m, tempitem)
                                SetPlayerInvItemValue(Index, m, 0)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = tempstr(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = tempstr(2)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = tempdata(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = tempdata(2)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = tempdata(3)

                                For i = 1 To Stats.Count - 1
                                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = tempdata(i + 3)
                                Next

                                tempitem = 0
                            End If

                            SendWornEquipment(Index)
                            SendMapEquipment(Index)
                            SendInventory(Index)
                            SendInventoryUpdate(Index, InvNum)
                            SendStats(Index)

                            ' send vitals
                            SendVitals(Index)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case EquipmentType.Armor

                            If GetPlayerEquipment(Index, EquipmentType.Armor) > 0 Then
                                tempitem = GetPlayerEquipment(Index, EquipmentType.Armor)
                                tempstr(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Prefix
                                tempstr(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Suffix
                                tempdata(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Damage
                                tempdata(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Speed
                                tempdata(3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Rarity
                                For i = 1 To Stats.Count - 1
                                    tempdata(i + 3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Stat(i)
                                Next
                            End If

                            SetPlayerEquipment(Index, InvItemNum, EquipmentType.Armor)

                            ' Transfer the Inventory data to the Equipment data
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Prefix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Suffix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Damage
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Speed
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Rarity

                            For i = 1 To Stats.Count - 1
                                Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Armor).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Stat(i)
                            Next

                            PlayerMsg(Index, "You equip " & CheckGrammar(Item(InvItemNum).Name), ColorType.BrightGreen)
                            TakeInvItem(Index, InvItemNum, 0)
                            ClearRandInv(Index, InvNum)

                            If tempitem > 0 Then ' Return their old equipment to their inventory.
                                m = FindOpenInvSlot(Index, tempitem)
                                SetPlayerInvItemNum(Index, m, tempitem)
                                SetPlayerInvItemValue(Index, m, 0)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = tempstr(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = tempstr(2)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = tempdata(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = tempdata(2)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = tempdata(3)

                                For i = 1 To Stats.Count - 1
                                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = tempdata(i + 3)
                                Next i

                                tempitem = 0
                            End If

                            SendWornEquipment(Index)
                            SendMapEquipment(Index)

                            SendInventory(Index)
                            SendStats(Index)

                            ' send vitals
                            SendVitals(Index)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case EquipmentType.Helmet

                            If GetPlayerEquipment(Index, EquipmentType.Helmet) > 0 Then
                                tempitem = GetPlayerEquipment(Index, EquipmentType.Helmet)
                                tempstr(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Prefix
                                tempstr(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Suffix
                                tempdata(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Damage
                                tempdata(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Speed
                                tempdata(3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Rarity
                                For i = 1 To Stats.Count - 1
                                    tempdata(i + 3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Stat(i)
                                Next i
                            End If

                            SetPlayerEquipment(Index, InvItemNum, EquipmentType.Helmet)

                            ' Transfer the Inventory data to the Equipment data
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Prefix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Suffix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Damage
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Speed
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Rarity

                            For i = 1 To Stats.Count - 1
                                Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Helmet).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Stat(i)
                            Next

                            PlayerMsg(Index, "You equip " & CheckGrammar(Item(InvItemNum).Name), ColorType.BrightGreen)
                            TakeInvItem(Index, InvItemNum, 1)
                            ClearRandInv(Index, InvNum)

                            If tempitem > 0 Then ' give back the stored item
                                m = FindOpenInvSlot(Index, tempitem)
                                SetPlayerInvItemNum(Index, m, tempitem)
                                SetPlayerInvItemValue(Index, m, 0)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = tempstr(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = tempstr(2)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = tempdata(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = tempdata(2)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = tempdata(3)

                                For i = 1 To Stats.Count - 1
                                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = tempdata(i + 3)
                                Next

                                tempitem = 0
                            End If

                            SendWornEquipment(Index)
                            SendMapEquipment(Index)
                            SendInventory(Index)
                            SendStats(Index)

                            ' send vitals
                            SendVitals(Index)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case EquipmentType.Shield
                            If Item(GetPlayerEquipment(Index, EquipmentType.Weapon)).TwoHanded > 0 Then
                                PlayerMsg(Index, "Please unequip your 2handed weapon first.", ColorType.BrightRed)
                                Exit Sub
                            End If

                            If GetPlayerEquipment(Index, EquipmentType.Shield) > 0 Then
                                tempitem = GetPlayerEquipment(Index, EquipmentType.Shield)
                                tempstr(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Prefix
                                tempstr(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Suffix
                                tempdata(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Damage
                                tempdata(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Speed
                                tempdata(3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Rarity
                                For i = 1 To Stats.Count - 1
                                    tempdata(i + 3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Stat(i)
                                Next i
                            End If

                            SetPlayerEquipment(Index, InvItemNum, EquipmentType.Shield)

                            ' Transfer the Inventory data to the Equipment data
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Prefix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Suffix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Damage
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Speed
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Rarity

                            For i = 1 To Stats.Count - 1
                                Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shield).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Stat(i)
                            Next

                            PlayerMsg(Index, "You equip " & CheckGrammar(Item(InvItemNum).Name), ColorType.BrightGreen)
                            TakeInvItem(Index, InvItemNum, 1)
                            ClearRandInv(Index, InvNum)

                            If tempitem > 0 Then ' give back the stored item
                                m = FindOpenInvSlot(Index, tempitem)
                                SetPlayerInvItemNum(Index, m, tempitem)
                                SetPlayerInvItemValue(Index, m, 0)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = tempstr(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = tempstr(2)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = tempdata(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = tempdata(2)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = tempdata(3)

                                For i = 1 To Stats.Count - 1
                                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = tempdata(i + 3)
                                Next

                                tempitem = 0
                            End If

                            SendWornEquipment(Index)
                            SendMapEquipment(Index)
                            SendInventory(Index)
                            SendStats(Index)

                            ' send vitals
                            SendVitals(Index)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case EquipmentType.Shoes
                            If GetPlayerEquipment(Index, EquipmentType.Shoes) > 0 Then
                                tempitem = GetPlayerEquipment(Index, EquipmentType.Shoes)
                                tempstr(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Prefix
                                tempstr(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Suffix
                                tempdata(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Damage
                                tempdata(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Speed
                                tempdata(3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Rarity
                                For i = 1 To Stats.Count - 1
                                    tempdata(i + 3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Stat(i)
                                Next i
                            End If

                            SetPlayerEquipment(Index, InvItemNum, EquipmentType.Shoes)

                            ' Transfer the Inventory data to the Equipment data
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Prefix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Suffix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Damage
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Speed
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Rarity

                            For i = 1 To Stats.Count - 1
                                Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Shoes).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Stat(i)
                            Next

                            PlayerMsg(Index, "You equip " & CheckGrammar(Item(InvItemNum).Name), ColorType.BrightGreen)
                            TakeInvItem(Index, InvItemNum, 1)
                            ClearRandInv(Index, InvNum)

                            If tempitem > 0 Then ' give back the stored item
                                m = FindOpenInvSlot(Index, tempitem)
                                SetPlayerInvItemNum(Index, m, tempitem)
                                SetPlayerInvItemValue(Index, m, 0)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = tempstr(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = tempstr(2)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = tempdata(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = tempdata(2)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = tempdata(3)

                                For i = 1 To Stats.Count - 1
                                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = tempdata(i + 3)
                                Next

                                tempitem = 0
                            End If

                            SendWornEquipment(Index)
                            SendMapEquipment(Index)
                            SendInventory(Index)
                            SendStats(Index)

                            ' send vitals
                            SendVitals(Index)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case EquipmentType.Gloves
                            If GetPlayerEquipment(Index, EquipmentType.Gloves) > 0 Then
                                tempitem = GetPlayerEquipment(Index, EquipmentType.Gloves)
                                tempstr(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Prefix
                                tempstr(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Suffix
                                tempdata(1) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Damage
                                tempdata(2) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Speed
                                tempdata(3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Rarity
                                For i = 1 To Stats.Count - 1
                                    tempdata(i + 3) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Stat(i)
                                Next i
                            End If

                            SetPlayerEquipment(Index, InvItemNum, EquipmentType.Gloves)

                            ' Transfer the Inventory data to the Equipment data
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Prefix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Suffix
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Damage
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Speed
                            Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Rarity

                            For i = 1 To Stats.Count - 1
                                Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EquipmentType.Gloves).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvNum).Stat(i)
                            Next

                            PlayerMsg(Index, "You equip " & CheckGrammar(Item(InvItemNum).Name), ColorType.BrightGreen)
                            TakeInvItem(Index, InvItemNum, 1)
                            ClearRandInv(Index, InvNum)

                            If tempitem > 0 Then ' give back the stored item
                                m = FindOpenInvSlot(Index, tempitem)
                                SetPlayerInvItemNum(Index, m, tempitem)
                                SetPlayerInvItemValue(Index, m, 0)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = tempstr(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = tempstr(2)

                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = tempdata(1)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = tempdata(2)
                                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = tempdata(3)

                                For i = 1 To Stats.Count - 1
                                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = tempdata(i + 3)
                                Next

                                tempitem = 0
                            End If

                            SendWornEquipment(Index)
                            SendMapEquipment(Index)
                            SendInventory(Index)
                            SendStats(Index)

                            ' send vitals
                            SendVitals(Index)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)
                    End Select

                Case ItemType.Consumable

                    For i = 1 To Stats.Count - 1
                        If GetPlayerStat(Index, i) < Item(InvItemNum).Stat_Req(i) Then
                            PlayerMsg(Index, "You do not meet the stat requirements to use this item.", ColorType.BrightRed)
                            Exit Sub
                        End If
                    Next

                    ' Make sure they are the right level
                    i = Item(InvItemNum).LevelReq

                    If i > GetPlayerLevel(Index) Then
                        PlayerMsg(Index, "You do not meet the level requirements to use this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    ' Make sure they are the right class
                    If Not Item(InvItemNum).ClassReq = GetPlayerClass(Index) And Not Item(InvItemNum).ClassReq = 0 Then
                        PlayerMsg(Index, "You do not meet the class requirements to use this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    ' access requirement
                    If Not GetPlayerAccess(Index) >= Item(InvItemNum).AccessReq Then
                        PlayerMsg(Index, "You do not meet the access requirement to use this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    'if that went fine, we progress the subtype

                    Select Case Item(InvItemNum).SubType
                        Case ConsumableType.Hp
                            SendActionMsg(GetPlayerMap(Index), "+" & Item(InvItemNum).Data1, ColorType.BrightGreen, ActionMsgType.Scroll, GetPlayerX(Index) * 32, GetPlayerY(Index) * 32)
                            SendAnimation(GetPlayerMap(Index), Item(InvItemNum).Animation, 0, 0, TargetType.Player, Index)
                            SetPlayerVital(Index, Vitals.HP, GetPlayerVital(Index, Vitals.HP) + Item(InvItemNum).Data1)
                            If Item(InvItemNum).Stackable = 1 Then
                                TakeInvItem(Index, InvItemNum, 1)
                            Else
                                TakeInvItem(Index, InvItemNum, 0)
                            End If
                            SendVital(Index, Vitals.HP)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case ConsumableType.Mp
                            SendActionMsg(GetPlayerMap(Index), "+" & Item(InvItemNum).Data1, ColorType.BrightBlue, ActionMsgType.Scroll, GetPlayerX(Index) * 32, GetPlayerY(Index) * 32)
                            SendAnimation(GetPlayerMap(Index), Item(InvItemNum).Animation, 0, 0, TargetType.Player, Index)
                            SetPlayerVital(Index, Vitals.MP, GetPlayerVital(Index, Vitals.MP) + Item(InvItemNum).Data1)
                            If Item(InvItemNum).Stackable = 1 Then
                                TakeInvItem(Index, InvItemNum, 1)
                            Else
                                TakeInvItem(Index, InvItemNum, 0)
                            End If
                            SendVital(Index, Vitals.MP)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case ConsumableType.Mp
                            SendAnimation(GetPlayerMap(Index), Item(InvItemNum).Animation, 0, 0, TargetType.Player, Index)
                            SetPlayerVital(Index, Vitals.SP, GetPlayerVital(Index, Vitals.SP) + Item(InvItemNum).Data1)
                            If Item(InvItemNum).Stackable = 1 Then
                                TakeInvItem(Index, InvItemNum, 1)
                            Else
                                TakeInvItem(Index, InvItemNum, 0)
                            End If
                            SendVital(Index, Vitals.SP)

                            ' send vitals to party if in one
                            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

                        Case ConsumableType.Exp

                    End Select

                Case ItemType.Key
                    InvItemNum = GetPlayerInvItemNum(Index, InvNum)

                    For i = 1 To Stats.Count - 1
                        If GetPlayerStat(Index, i) < Item(InvItemNum).Stat_Req(i) Then
                            PlayerMsg(Index, "You do not meet the stat requirements to use this item.", ColorType.BrightRed)
                            Exit Sub
                        End If
                    Next

                    ' Make sure they are the right level
                    i = Item(InvItemNum).LevelReq

                    If i > GetPlayerLevel(Index) Then
                        PlayerMsg(Index, "You do not meet the level requirements to use this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    ' Make sure they are the right class
                    If Not Item(InvItemNum).ClassReq = GetPlayerClass(Index) And Not Item(InvItemNum).ClassReq = 0 Then
                        PlayerMsg(Index, "You do not meet the class requirements to use this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    Select Case GetPlayerDir(Index)
                        Case Direction.Up

                            If GetPlayerY(Index) > 0 Then
                                x = GetPlayerX(Index)
                                y = GetPlayerY(Index) - 1
                            Else
                                Exit Sub
                            End If

                        Case Direction.Down

                            If GetPlayerY(Index) < Map(GetPlayerMap(Index)).MaxY Then
                                x = GetPlayerX(Index)
                                y = GetPlayerY(Index) + 1
                            Else
                                Exit Sub
                            End If

                        Case Direction.Left

                            If GetPlayerX(Index) > 0 Then
                                x = GetPlayerX(Index) - 1
                                y = GetPlayerY(Index)
                            Else
                                Exit Sub
                            End If

                        Case Direction.Right

                            If GetPlayerX(Index) < Map(GetPlayerMap(Index)).MaxX Then
                                x = GetPlayerX(Index) + 1
                                y = GetPlayerY(Index)
                            Else
                                Exit Sub
                            End If

                    End Select

                    ' Check if a key exists
                    If Map(GetPlayerMap(Index)).Tile(x, y).Type = TileType.Key Then

                        ' Check if the key they are using matches the map key
                        If InvItemNum = Map(GetPlayerMap(Index)).Tile(x, y).Data1 Then
                            TempTile(GetPlayerMap(Index)).DoorOpen(x, y) = True
                            TempTile(GetPlayerMap(Index)).DoorTimer = GetTimeMs()
                            SendMapKey(Index, x, y, 1)
                            MapMsg(GetPlayerMap(Index), "A door has been unlocked.", ColorType.Yellow)

                            SendAnimation(GetPlayerMap(Index), Item(InvItemNum).Animation, x, y)

                            ' Check if we are supposed to take away the item
                            If Map(GetPlayerMap(Index)).Tile(x, y).Data2 = 1 Then
                                TakeInvItem(Index, InvItemNum, 0)
                                PlayerMsg(Index, "The key is destroyed in the lock.", ColorType.Yellow)
                            End If
                        End If
                    End If

                Case ItemType.Skill
                    InvItemNum = GetPlayerInvItemNum(Index, InvNum)

                    For i = 1 To Stats.Count - 1
                        If GetPlayerStat(Index, i) < Item(InvItemNum).Stat_Req(i) Then
                            PlayerMsg(Index, "You do not meet the stat requirements to use this item.", ColorType.BrightRed)
                            Exit Sub
                        End If
                    Next

                    ' Make sure they are the right class
                    If Not Item(InvItemNum).ClassReq = GetPlayerClass(Index) And Not Item(InvItemNum).ClassReq = 0 Then
                        PlayerMsg(Index, "You do not meet the class requirements to use this item.", ColorType.BrightRed)
                        Exit Sub
                    End If

                    ' Get the skill num
                    n = Item(InvItemNum).Data1

                    If n > 0 Then

                        ' Make sure they are the right class
                        If Skill(n).ClassReq = GetPlayerClass(Index) Or Skill(n).ClassReq = 0 Then
                            ' Make sure they are the right level
                            i = Skill(n).LevelReq

                            If i <= GetPlayerLevel(Index) Then
                                i = FindOpenSkillSlot(Index)

                                ' Make sure they have an open skill slot
                                If i > 0 Then

                                    ' Make sure they dont already have the skill
                                    If Not HasSkill(Index, n) Then
                                        SetPlayerSkill(Index, i, n)
                                        SendAnimation(GetPlayerMap(Index), Item(InvItemNum).Animation, 0, 0, TargetType.Player, Index)
                                        TakeInvItem(Index, InvItemNum, 0)
                                        PlayerMsg(Index, "You study the skill carefully.", ColorType.Yellow)
                                        PlayerMsg(Index, "You have learned a new skill!", ColorType.BrightGreen)
                                    Else
                                        PlayerMsg(Index, "You have already learned this skill!", ColorType.BrightRed)
                                    End If

                                Else
                                    PlayerMsg(Index, "You have learned all that you can learn!", ColorType.BrightRed)
                                End If

                            Else
                                PlayerMsg(Index, "You must be level " & i & " to learn this skill.", ColorType.Yellow)
                            End If

                        Else
                            PlayerMsg(Index, "This skill can only be learned by " & CheckGrammar(GetClassName(Skill(n).ClassReq)) & ".", ColorType.Yellow)
                        End If

                    Else
                        PlayerMsg(Index, "This scroll is not connected to a skill, please inform an admin!", ColorType.BrightRed)
                    End If
                Case ItemType.Furniture
                    PlayerMsg(Index, "To place furniture, simply click on it in your inventory, then click in your house where you want it.", ColorType.Yellow)

                Case ItemType.Recipe

                    PlayerMsg(Index, "Lets learn this recipe :)", ColorType.BrightGreen)
                    ' Get the recipe num
                    n = Item(InvItemNum).Data1
                    LearnRecipe(Index, n, InvNum)
                Case ItemType.Pet
                    If Item(InvItemNum).Stackable = 1 Then
                        TakeInvItem(Index, InvItemNum, 1)
                    Else
                        TakeInvItem(Index, InvItemNum, 0)
                    End If
                    n = Item(InvItemNum).Data1
                    AdoptPet(Index, n)
            End Select

        End If
    End Sub

    Sub PlayerSwitchInvSlots(ByVal Index As Integer, ByVal OldSlot As Integer, ByVal NewSlot As Integer)
        Dim OldNum As Integer, OldValue As Integer, OldRarity As Integer, OldPrefix As String
        Dim OldSuffix As String, OldSpeed As Integer, OldDamage As Integer
        Dim NewNum As Integer, NewValue As Integer, NewRarity As Integer, NewPrefix As String
        Dim NewSuffix As String, NewSpeed As Integer, NewDamage As Integer
        Dim NewStats(0 To Stats.Count - 1) As Integer
        Dim OldStats(0 To Stats.Count - 1) As Integer

        If OldSlot = 0 Or NewSlot = 0 Then Exit Sub

        OldNum = GetPlayerInvItemNum(Index, OldSlot)
        OldValue = GetPlayerInvItemValue(Index, OldSlot)
        NewNum = GetPlayerInvItemNum(Index, NewSlot)
        NewValue = GetPlayerInvItemValue(Index, NewSlot)

        If OldNum = NewNum And Item(NewNum).Stackable = 1 Then ' same item, if we can stack it, lets do that :P
            SetPlayerInvItemNum(Index, NewSlot, NewNum)
            SetPlayerInvItemValue(Index, NewSlot, OldValue + NewValue)
            SetPlayerInvItemNum(Index, OldSlot, 0)
            SetPlayerInvItemValue(Index, OldSlot, 0)
        Else
            SetPlayerInvItemNum(Index, NewSlot, OldNum)
            SetPlayerInvItemValue(Index, NewSlot, OldValue)
            SetPlayerInvItemNum(Index, OldSlot, NewNum)
            SetPlayerInvItemValue(Index, OldSlot, NewValue)
        End If

        ' RandomInv
        With Player(Index).Character(TempPlayer(Index).CurChar).RandInv(NewSlot)
            NewPrefix = .Prefix
            NewSuffix = .Suffix
            NewDamage = .Damage
            NewSpeed = .Speed
            NewRarity = .Rarity
            For i = 1 To Stats.Count - 1
                NewStats(i) = .Stat(i)
            Next i
        End With

        With Player(Index).Character(TempPlayer(Index).CurChar).RandInv(OldSlot)
            OldPrefix = .Prefix
            OldSuffix = .Suffix
            OldDamage = .Damage
            OldSpeed = .Speed
            OldRarity = .Rarity
            For i = 1 To Stats.Count - 1
                OldStats(i) = .Stat(i)
            Next i
        End With

        With Player(Index).Character(TempPlayer(Index).CurChar).RandInv(NewSlot)
            .Prefix = OldPrefix
            .Suffix = OldSuffix
            .Damage = OldDamage
            .Speed = OldSpeed
            .Rarity = OldRarity
            For i = 1 To Stats.Count - 1
                .Stat(i) = OldStats(i)
            Next i
        End With

        With Player(Index).Character(TempPlayer(Index).CurChar).RandInv(OldSlot)
            .Prefix = NewPrefix
            .Suffix = NewSuffix
            .Damage = NewDamage
            .Speed = NewSpeed
            .Rarity = NewRarity
            For i = 1 To Stats.Count - 1
                .Stat(i) = NewStats(i)
            Next i
        End With

        SendInventory(Index)
    End Sub

#End Region

#Region "Equipment"
    Sub CheckEquippedItems(ByVal Index As Integer)
        Dim itemNum As Integer
        Dim i As Integer

        ' We want to check incase an admin takes away an object but they had it equipped
        For i = 1 To EquipmentType.Count - 1
            itemNum = GetPlayerEquipment(Index, i)

            If itemNum > 0 Then

                Select Case i
                    Case EquipmentType.Weapon

                        If Item(itemNum).SubType <> EquipmentType.Weapon Then SetPlayerEquipment(Index, 0, i)
                    Case EquipmentType.Armor

                        If Item(itemNum).SubType <> EquipmentType.Armor Then SetPlayerEquipment(Index, 0, i)
                    Case EquipmentType.Helmet

                        If Item(itemNum).SubType <> EquipmentType.Helmet Then SetPlayerEquipment(Index, 0, i)
                    Case EquipmentType.Shield

                        If Item(itemNum).SubType <> EquipmentType.Shield Then SetPlayerEquipment(Index, 0, i)
                    Case EquipmentType.Shoes

                        If Item(itemNum).SubType <> EquipmentType.Shoes Then SetPlayerEquipment(Index, 0, i)
                    Case EquipmentType.Gloves

                        If Item(itemNum).SubType <> EquipmentType.Gloves Then SetPlayerEquipment(Index, 0, i)
                End Select

            Else
                SetPlayerEquipment(Index, 0, i)
            End If

        Next

    End Sub

    Sub PlayerUnequipItem(ByVal Index As Integer, ByVal EqSlot As Integer)
        Dim i As Integer, m As Integer, itemnum As Integer

        If EqSlot <= 0 Or EqSlot > EquipmentType.Count - 1 Then Exit Sub ' exit out early if error'd

        If FindOpenInvSlot(Index, GetPlayerEquipment(Index, EqSlot)) > 0 Then
            itemnum = GetPlayerEquipment(Index, EqSlot)

            m = FindOpenInvSlot(Index, Player(Index).Character(TempPlayer(Index).CurChar).Equipment(EqSlot))
            SetPlayerInvItemNum(Index, m, Player(Index).Character(TempPlayer(Index).CurChar).Equipment(EqSlot))
            SetPlayerInvItemValue(Index, m, 0)

            Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EqSlot).Prefix
            Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EqSlot).Suffix
            Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EqSlot).Damage
            Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EqSlot).Speed
            Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EqSlot).Rarity
            For i = 1 To Stats.Count - 1
                Player(Index).Character(TempPlayer(Index).CurChar).RandInv(m).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandEquip(EqSlot).Stat(i)
            Next

            ClearRandEq(Index, EqSlot)

            PlayerMsg(Index, "You unequip " & CheckGrammar(Item(GetPlayerEquipment(Index, EqSlot)).Name), ColorType.Yellow)
            ' remove equipment
            SetPlayerEquipment(Index, 0, EqSlot)
            SendWornEquipment(Index)
            SendMapEquipment(Index)
            SendStats(Index)
            SendInventory(Index)
            ' send vitals
            SendVitals(Index)

            ' send vitals to party if in one
            If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)
        Else
            PlayerMsg(Index, "Your inventory is full.", ColorType.BrightRed)
        End If

    End Sub
#End Region

#Region "Misc"
    Sub JoinGame(ByVal Index As Integer)
        Dim i As Integer

        ' Set the flag so we know the person is in the game
        TempPlayer(Index).InGame = True

        ' Notify everyone that a player has joined the game.
        GlobalMsg(String.Format("{0} has joined {1}!", GetPlayerName(Index), Options.GameName))

        ' Send an ok to client to start receiving in game data
        SendLoadCharOk(Index)

        ' Set some data related to housing instances.
        If Player(Index).Character(TempPlayer(Index).CurChar).InHouse Then
            Player(Index).Character(TempPlayer(Index).CurChar).InHouse = 0
            Player(Index).Character(TempPlayer(Index).CurChar).X = Player(Index).Character(TempPlayer(Index).CurChar).LastX
            Player(Index).Character(TempPlayer(Index).CurChar).Y = Player(Index).Character(TempPlayer(Index).CurChar).LastY
            Player(Index).Character(TempPlayer(Index).CurChar).Map = Player(Index).Character(TempPlayer(Index).CurChar).LastMap
        End If

        ' Send all the required game data to the user.
        SendTotalOnlineTo(Index)
        CheckEquippedItems(Index)
        SendGameData(Index)
        SendInventory(Index)
        SendWornEquipment(Index)
        SendMapEquipment(Index)
        SendProjectiles(Index)
        SendVitals(Index)
        SendExp(Index)
        SendQuests(Index)
        SendPlayerQuests(Index)
        SendMapNames(Index)
        SendHotbar(Index)
        SendPlayerSkills(Index)
        SendRecipes(Index)
        SendStats(Index)
        SendJoinMap(Index)
        SendHouseConfigs(Index)
        SendPets(Index)
        SendUpdatePlayerPet(Index, True)
        SendTimeTo(Index)
        SendGameClockTo(Index)

        For i = 0 To ResourceCache(GetPlayerMap(Index)).Resource_Count
            SendResourceCacheTo(Index, i)
        Next

        SendTotalOnlineToAll()

        ' Warp the player to his saved location
        PlayerWarp(Index, GetPlayerMap(Index), GetPlayerX(Index), GetPlayerY(Index))

        ' Send welcome messages
        SendWelcome(Index)

        ' Send the flag so they know they can start doing stuff
        SendInGame(Index)

        UpdateCaption()
    End Sub

    Sub LeftGame(ByVal Index As Integer)
        Dim i As Integer
        Dim tradeTarget As Integer

        If TempPlayer(Index).InGame Then
            SendLeftMap(Index)
            TempPlayer(Index).InGame = False

            ' Check if player was the only player on the map and stop npc processing if so
            If GetPlayerMap(Index) > 0 Then
                If GetTotalMapPlayers(GetPlayerMap(Index)) < 1 Then
                    PlayersOnMap(GetPlayerMap(Index)) = False
                    If IsInstancedMap(GetPlayerMap(Index)) Then
                        DestroyInstancedMap(GetPlayerMap(Index) - MAX_MAPS)

                        If TempPlayer(Index).InInstance = 1 Then
                            SetPlayerMap(Index, TempPlayer(Index).TmpMap)
                            SetPlayerX(Index, TempPlayer(Index).TmpX)
                            SetPlayerY(Index, TempPlayer(Index).TmpY)
                            TempPlayer(Index).InInstance = 0
                        End If
                    End If
                End If
            End If

            ' Check if the player was in a party, and if so cancel it out so the other player doesn't continue to get half exp
            ' leave party.
            Party_PlayerLeave(Index)

            ' cancel any trade they're in
            If TempPlayer(Index).InTrade > 0 Then
                tradeTarget = TempPlayer(Index).InTrade
                PlayerMsg(tradeTarget, String.Format("{0} has declined the trade.", GetPlayerName(Index)), ColorType.BrightRed)
                ' clear out trade
                For i = 1 To MAX_INV
                    TempPlayer(tradeTarget).TradeOffer(i).Num = 0
                    TempPlayer(tradeTarget).TradeOffer(i).Value = 0
                Next
                TempPlayer(tradeTarget).InTrade = 0
                SendCloseTrade(tradeTarget)
            End If

            'pet
            'ReleasePet(Index)
            ReCallPet(Index)

            SavePlayer(Index)
            SaveBank(Index)

            ' Send a global message that he/she left
            GlobalMsg(String.Format("{0} has left {1}!", GetPlayerName(Index), Options.GameName))

            TextAdd(String.Format("{0} has left {1}!", GetPlayerName(Index), Options.GameName))


            TempPlayer(Index) = Nothing
            ReDim TempPlayer(i).SkillCD(MAX_PLAYER_SKILLS)
            ReDim TempPlayer(i).TradeOffer(MAX_INV)
        End If

        SendTotalOnlineToAll()

        ClearPlayer(Index)
        ClearBank(Index)

        UpdateCaption()
    End Sub

    Public Sub KillPlayer(ByVal Index As Integer)
        Dim exp As Integer

        ' Calculate exp to give attacker
        exp = GetPlayerExp(Index) \ 3

        ' Make sure we dont get less then 0
        If exp < 0 Then exp = 0
        If exp = 0 Then
            PlayerMsg(Index, "You've lost no experience.", ColorType.BrightGreen)
        Else
            SetPlayerExp(Index, GetPlayerExp(Index) - exp)
            SendExp(Index)
            PlayerMsg(Index, String.Format("You've lost {0} experience.", exp), ColorType.BrightRed)
        End If

        OnDeath(Index)
    End Sub

    Sub OnDeath(ByVal Index As Integer)
        'Dim i As Integer

        ' Set HP to nothing
        SetPlayerVital(Index, Vitals.HP, 0)

        ' Warp player away
        SetPlayerDir(Index, Direction.Down)

        With Map(GetPlayerMap(Index))
            ' to the bootmap if it is set
            If .BootMap > 0 Then
                PlayerWarp(Index, .BootMap, .BootX, .BootY)
            Else
                PlayerWarp(Index, Options.StartMap, Options.StartX, Options.StartY)
            End If
        End With

        ' Clear skill casting
        TempPlayer(Index).SkillBuffer = 0
        TempPlayer(Index).SkillBufferTimer = 0
        SendClearSkillBuffer(Index)

        ' Restore vitals
        SetPlayerVital(Index, Vitals.HP, GetPlayerMaxVital(Index, Vitals.HP))
        SetPlayerVital(Index, Vitals.MP, GetPlayerMaxVital(Index, Vitals.MP))
        SetPlayerVital(Index, Vitals.SP, GetPlayerMaxVital(Index, Vitals.SP))
        SendVitals(Index)

        ' send vitals to party if in one
        If TempPlayer(Index).InParty > 0 Then SendPartyVitals(TempPlayer(Index).InParty, Index)

        ' If the player the attacker killed was a pk then take it away
        If GetPlayerPK(Index) = True Then
            SetPlayerPK(Index, False)
            SendPlayerData(Index)
        End If

    End Sub

    Function GetPlayerVitalRegen(ByVal Index As Integer, ByVal Vital As Vitals) As Integer
        Dim i As Integer

        ' Prevent subscript out of range
        If IsPlaying(Index) = False Or Index <= 0 Or Index > MAX_PLAYERS Then
            GetPlayerVitalRegen = 0
            Exit Function
        End If

        Select Case Vital
            Case Vitals.HP
                i = (GetPlayerStat(Index, Stats.Vitality) \ 2)
            Case Vitals.MP
                i = (GetPlayerStat(Index, Stats.Spirit) \ 2)
            Case Vitals.SP
                i = (GetPlayerStat(Index, Stats.Spirit) \ 2)
        End Select

        If i < 2 Then i = 2
        GetPlayerVitalRegen = i
    End Function

    Public Sub HandleNpcKillExperience(ByVal Index As Integer, ByVal NpcNum As Integer)
        ' Get the experience we'll have to hand out. If it's negative, just ignore this method.
        Dim Experience = Npc(NpcNum).Exp
        If Experience <= 0 Then Exit Sub

        ' Is our player in a party? If so, hand out exp to everyone.
        If IsPlayerInParty(Index) Then
            Party_ShareExp(GetPlayerParty(Index), Experience, Index, GetPlayerMap(Index))
        Else
            GivePlayerEXP(Index, Experience)
        End If
    End Sub

    Public Sub HandlePlayerKillExperience(ByVal Attacker As Integer, ByVal Victim As Integer)
        ' Calculate exp to give attacker
        Dim exp = (GetPlayerExp(Victim) \ 10)

        ' Make sure we dont get less then 0
        If exp < 0 Then
            exp = 0
        End If

        If exp = 0 Then
            PlayerMsg(Victim, "You've lost no exp.", ColorType.BrightRed)
            PlayerMsg(Attacker, "You've received no exp.", ColorType.BrightBlue)
        Else
            SetPlayerExp(Victim, GetPlayerExp(Victim) - exp)
            SendExp(Victim)
            PlayerMsg(Victim, String.Format("You've lost {0} exp.", exp), ColorType.BrightRed)

            ' check if we're in a party
            If IsPlayerInParty(Attacker) > 0 Then
                ' pass through party exp share function
                Party_ShareExp(GetPlayerParty(Attacker), exp, Attacker, GetPlayerMap(Attacker))
            Else
                ' not in party, get exp for self
                GivePlayerEXP(Attacker, exp)
            End If
        End If
    End Sub
#End Region

#Region "Skills"
    Function FindOpenSkillSlot(ByVal Index As Integer) As Integer
        Dim i As Integer

        For i = 1 To MAX_PLAYER_SKILLS

            If GetPlayerSkill(Index, i) = 0 Then
                FindOpenSkillSlot = i
                Exit Function
            End If

        Next

    End Function

    Function GetPlayerSkill(ByVal Index As Integer, ByVal Skillslot As Integer) As Integer
        GetPlayerSkill = 0
        If Index > MAX_PLAYERS Then Exit Function

        GetPlayerSkill = Player(Index).Character(TempPlayer(Index).CurChar).Skill(Skillslot)
    End Function

    Public Function GetPlayerSkillSlot(ByVal Index As Integer, ByVal SkillId As Integer) As Integer
        GetPlayerSkillSlot = -1
        If Index < 0 Or Index > MAX_PLAYERS Then Exit Function
        Dim data = Player(Index).Character(TempPlayer(Index).CurChar).Skill.Where(Function(x) x = SkillId).ToArray()
        If data.Length > 0 Then
            GetPlayerSkillSlot = data.Single()
        End If
    End Function

    Function HasSkill(ByVal Index As Integer, ByVal Skillnum As Integer) As Boolean
        Dim i As Integer

        For i = 1 To MAX_PLAYER_SKILLS

            If GetPlayerSkill(Index, i) = Skillnum Then
                HasSkill = True
                Exit Function
            End If

        Next

    End Function

    Sub SetPlayerSkill(ByVal Index As Integer, ByVal Skillslot As Integer, ByVal Skillnum As Integer)
        Player(Index).Character(TempPlayer(Index).CurChar).Skill(Skillslot) = Skillnum
    End Sub

    Public Sub BufferSkill(ByVal Index As Integer, ByVal Skillslot As Integer)
        Dim skillnum As Integer
        Dim MPCost As Integer
        Dim LevelReq As Integer
        Dim MapNum As Integer
        Dim SkillCastType As Integer
        Dim ClassReq As Integer
        Dim AccessReq As Integer
        Dim range As Integer
        Dim HasBuffered As Boolean

        Dim TargetType As TargetType
        Dim Target As Integer

        ' Prevent subscript out of range
        If Skillslot <= 0 Or Skillslot > MAX_PLAYER_SKILLS Then Exit Sub

        skillnum = GetPlayerSkill(Index, Skillslot)
        MapNum = GetPlayerMap(Index)

        If skillnum <= 0 Or skillnum > MAX_SKILLS Then Exit Sub

        ' Make sure player has the skill
        If Not HasSkill(Index, skillnum) Then Exit Sub

        ' see if cooldown has finished
        If TempPlayer(Index).SkillCD(Skillslot) > GetTimeMs() Then
            PlayerMsg(Index, "Skill hasn't cooled down yet!", ColorType.Yellow)
            Exit Sub
        End If

        MPCost = Skill(skillnum).MpCost

        ' Check if they have enough MP
        If GetPlayerVital(Index, Vitals.MP) < MPCost Then
            PlayerMsg(Index, "Not enough mana!", ColorType.Yellow)
            Exit Sub
        End If

        LevelReq = Skill(skillnum).LevelReq

        ' Make sure they are the right level
        If LevelReq > GetPlayerLevel(Index) Then
            PlayerMsg(Index, "You must be level " & LevelReq & " to use this skill.", ColorType.BrightRed)
            Exit Sub
        End If

        AccessReq = Skill(skillnum).AccessReq

        ' make sure they have the right access
        If AccessReq > GetPlayerAccess(Index) Then
            PlayerMsg(Index, "You must be an administrator to use this skill.", ColorType.BrightRed)
            Exit Sub
        End If

        ClassReq = Skill(skillnum).ClassReq

        ' make sure the classreq > 0
        If ClassReq > 0 Then ' 0 = no req
            If ClassReq <> GetPlayerClass(Index) Then
                PlayerMsg(Index, "Only " & CheckGrammar(Trim$(Classes(ClassReq).Name)) & " can use this skill.", ColorType.Yellow)
                Exit Sub
            End If
        End If

        ' find out what kind of skill it is! self cast, target or AOE
        If Skill(skillnum).Range > 0 Then
            ' ranged attack, single target or aoe?
            If Not Skill(skillnum).IsAoE Then
                SkillCastType = 2 ' targetted
            Else
                SkillCastType = 3 ' targetted aoe
            End If
        Else
            If Not Skill(skillnum).IsAoE Then
                SkillCastType = 0 ' self-cast
            Else
                SkillCastType = 1 ' self-cast AoE
            End If
        End If

        TargetType = TempPlayer(Index).TargetType
        Target = TempPlayer(Index).Target
        range = Skill(skillnum).Range
        HasBuffered = False

        Select Case SkillCastType
            Case 0, 1 ' self-cast & self-cast AOE
                HasBuffered = True
            Case 2, 3 ' targeted & targeted AOE
                ' check if have target
                If Not Target > 0 Then
                    PlayerMsg(Index, "You do not have a target.", ColorType.BrightRed)
                End If
                If TargetType = TargetType.Player Then
                    'Housing
                    If Player(Target).Character(TempPlayer(Target).CurChar).InHouse = Player(Index).Character(TempPlayer(Index).CurChar).InHouse Then
                        If CanPlayerAttackPlayer(Index, Target, True) Then
                            HasBuffered = True
                        End If
                    End If
                    ' if have target, check in range
                    If Not IsInRange(range, GetPlayerX(Index), GetPlayerY(Index), GetPlayerX(Target), GetPlayerY(Target)) Then
                        PlayerMsg(Index, "Target not in range.", ColorType.BrightRed)
                    Else
                        ' go through skill types
                        If Skill(skillnum).Type <> SkillType.DamageHp And Skill(skillnum).Type <> SkillType.DamageMp Then
                            HasBuffered = True
                        Else
                            If CanPlayerAttackPlayer(Index, Target, True) Then
                                HasBuffered = True
                            End If
                        End If
                    End If
                ElseIf TargetType = TargetType.Npc Then
                    ' if have target, check in range
                    If Not IsInRange(range, GetPlayerX(Index), GetPlayerY(Index), MapNpc(MapNum).Npc(Target).X, MapNpc(MapNum).Npc(Target).Y) Then
                        PlayerMsg(Index, "Target not in range.", ColorType.BrightRed)
                        HasBuffered = False
                    Else
                        ' go through skill types
                        If Skill(skillnum).Type <> SkillType.DamageHp And Skill(skillnum).Type <> SkillType.DamageMp Then
                            HasBuffered = True
                        Else
                            If CanPlayerAttackNpc(Index, Target, True) Then
                                HasBuffered = True
                            End If
                        End If
                    End If
                End If
        End Select

        If HasBuffered Then
            SendAnimation(MapNum, Skill(skillnum).CastAnim, 0, 0, TargetType.Player, Index)
            TempPlayer(Index).SkillBuffer = Skillslot
            TempPlayer(Index).SkillBufferTimer = GetTimeMs()
            Exit Sub
        Else
            SendClearSkillBuffer(Index)
        End If
    End Sub
#End Region

#Region "Bank"
    Sub GiveBankItem(ByVal Index As Integer, ByVal InvSlot As Integer, ByVal Amount As Integer)
        Dim BankSlot As Integer, itemnum As Integer

        If InvSlot < 0 Or InvSlot > MAX_INV Then Exit Sub

        If GetPlayerInvItemValue(Index, InvSlot) < 0 Then Exit Sub
        If GetPlayerInvItemValue(Index, InvSlot) < Amount Then Exit Sub

        BankSlot = FindOpenBankSlot(Index, GetPlayerInvItemNum(Index, InvSlot))
        itemnum = GetPlayerInvItemNum(Index, InvSlot)

        If BankSlot > 0 Then
            If Item(GetPlayerInvItemNum(Index, InvSlot)).Type = ItemType.Currency Or Item(GetPlayerInvItemNum(Index, InvSlot)).Stackable = 1 Then
                If GetPlayerBankItemNum(Index, BankSlot) = GetPlayerInvItemNum(Index, InvSlot) Then
                    SetPlayerBankItemValue(Index, BankSlot, GetPlayerBankItemValue(Index, BankSlot) + Amount)
                    TakeInvItem(Index, GetPlayerInvItemNum(Index, InvSlot), Amount)
                Else
                    SetPlayerBankItemNum(Index, BankSlot, GetPlayerInvItemNum(Index, InvSlot))
                    SetPlayerBankItemValue(Index, BankSlot, Amount)
                    TakeInvItem(Index, GetPlayerInvItemNum(Index, InvSlot), Amount)
                End If
            Else
                If GetPlayerBankItemNum(Index, BankSlot) = GetPlayerInvItemNum(Index, InvSlot) And Item(itemnum).Randomize = 0 Then
                    SetPlayerBankItemValue(Index, BankSlot, GetPlayerBankItemValue(Index, BankSlot) + 1)
                    TakeInvItem(Index, GetPlayerInvItemNum(Index, InvSlot), 0)
                Else
                    Bank(Index).ItemRand(BankSlot).Prefix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvSlot).Prefix
                    Bank(Index).ItemRand(BankSlot).Suffix = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvSlot).Suffix
                    Bank(Index).ItemRand(BankSlot).Rarity = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvSlot).Rarity
                    Bank(Index).ItemRand(BankSlot).Damage = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvSlot).Damage
                    Bank(Index).ItemRand(BankSlot).Speed = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvSlot).Speed

                    For i = 1 To Stats.Count - 1
                        Bank(Index).ItemRand(BankSlot).Stat(i) = Player(Index).Character(TempPlayer(Index).CurChar).RandInv(InvSlot).Stat(i)
                    Next

                    SetPlayerBankItemNum(Index, BankSlot, itemnum)
                    SetPlayerBankItemValue(Index, BankSlot, 1)
                    ClearRandInv(Index, InvSlot)
                    TakeInvItem(Index, GetPlayerInvItemNum(Index, InvSlot), 0)
                End If
            End If
        End If

        SaveBank(Index)
        SavePlayer(Index)
        SendBank(Index)

    End Sub

    Function GetPlayerBankItemNum(ByVal Index As Integer, ByVal BankSlot As Byte) As Integer
        GetPlayerBankItemNum = Bank(Index).Item(BankSlot).Num
    End Function

    Sub SetPlayerBankItemNum(ByVal Index As Integer, ByVal BankSlot As Byte, ByVal ItemNum As Integer)
        Bank(Index).Item(BankSlot).Num = ItemNum
    End Sub

    Function GetPlayerBankItemValue(ByVal Index As Integer, ByVal BankSlot As Byte) As Integer
        GetPlayerBankItemValue = Bank(Index).Item(BankSlot).Value
    End Function

    Sub SetPlayerBankItemValue(ByVal Index As Integer, ByVal BankSlot As Byte, ByVal ItemValue As Integer)
        Bank(Index).Item(BankSlot).Value = ItemValue
    End Sub

    Function FindOpenBankSlot(ByVal Index As Integer, ByVal ItemNum As Integer) As Byte
        Dim i As Integer

        If Not IsPlaying(Index) Then Exit Function
        If ItemNum <= 0 Or ItemNum > MAX_ITEMS Then Exit Function

        If Item(ItemNum).Type = ItemType.Currency Or Item(ItemNum).Stackable = 1 Then
            For i = 1 To MAX_BANK
                If GetPlayerBankItemNum(Index, i) = ItemNum Then
                    FindOpenBankSlot = i
                    Exit Function
                End If
            Next
        End If

        For i = 1 To MAX_BANK
            If GetPlayerBankItemNum(Index, i) = 0 Then
                FindOpenBankSlot = i
                Exit Function
            End If
        Next

    End Function

    Sub TakeBankItem(ByVal Index As Integer, ByVal BankSlot As Integer, ByVal Amount As Integer)
        Dim invSlot

        If BankSlot < 0 Or BankSlot > MAX_BANK Then Exit Sub

        If GetPlayerBankItemValue(Index, BankSlot) < 0 Then Exit Sub

        If GetPlayerBankItemValue(Index, BankSlot) < Amount Then Exit Sub

        invSlot = FindOpenInvSlot(Index, GetPlayerBankItemNum(Index, BankSlot))

        If invSlot > 0 Then
            If Item(GetPlayerBankItemNum(Index, BankSlot)).Type = ItemType.Currency Or Item(GetPlayerBankItemNum(Index, BankSlot)).Stackable = 1 Then
                GiveInvItem(Index, GetPlayerBankItemNum(Index, BankSlot), Amount)
                SetPlayerBankItemValue(Index, BankSlot, GetPlayerBankItemValue(Index, BankSlot) - Amount)
                If GetPlayerBankItemValue(Index, BankSlot) <= 0 Then
                    SetPlayerBankItemNum(Index, BankSlot, 0)
                    SetPlayerBankItemValue(Index, BankSlot, 0)
                End If
            Else
                If GetPlayerBankItemNum(Index, BankSlot) = GetPlayerInvItemNum(Index, invSlot) And Item(GetPlayerBankItemNum(Index, BankSlot)).Randomize = 0 Then
                    If GetPlayerBankItemValue(Index, BankSlot) > 1 Then
                        GiveInvItem(Index, GetPlayerBankItemNum(Index, BankSlot), 0)
                        SetPlayerBankItemValue(Index, BankSlot, GetPlayerBankItemValue(Index, BankSlot) - 1)

                    End If
                Else
                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(invSlot).Prefix = Bank(Index).ItemRand(BankSlot).Prefix
                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(invSlot).Suffix = Bank(Index).ItemRand(BankSlot).Suffix
                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(invSlot).Rarity = Bank(Index).ItemRand(BankSlot).Rarity
                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(invSlot).Damage = Bank(Index).ItemRand(BankSlot).Damage
                    Player(Index).Character(TempPlayer(Index).CurChar).RandInv(invSlot).Speed = Bank(Index).ItemRand(BankSlot).Speed
                    For i = 1 To Stats.Count - 1
                        Player(Index).Character(TempPlayer(Index).CurChar).RandInv(invSlot).Stat(i) = Bank(Index).ItemRand(BankSlot).Stat(i)
                    Next i

                    GiveInvItem(Index, GetPlayerBankItemNum(Index, BankSlot), 0)
                    SetPlayerBankItemNum(Index, BankSlot, 0)
                    SetPlayerBankItemValue(Index, BankSlot, 0)
                    ClearRandBank(Index, BankSlot)

                End If
            End If

        End If

        SaveBank(Index)
        SavePlayer(Index)
        SendBank(Index)

    End Sub

    Sub PlayerSwitchBankSlots(ByVal Index As Integer, ByVal OldSlot As Integer, ByVal NewSlot As Integer)
        Dim OldNum As Integer, OldValue As Integer, NewNum As Integer, NewValue As Integer
        Dim i As Integer, NewStats() As Integer, OldStats() As Integer
        Dim NewRarity As Integer, OldRarity As Integer, NewPrefix As String, OldPrefix As String, NewSuffix As String
        Dim OldSuffix As String, NewSpeed As Integer, OldSpeed As Integer, NewDamage As Integer, OldDamage As Integer

        If OldSlot = 0 Or NewSlot = 0 Then Exit Sub

        OldNum = GetPlayerBankItemNum(Index, OldSlot)
        OldValue = GetPlayerBankItemValue(Index, OldSlot)
        NewNum = GetPlayerBankItemNum(Index, NewSlot)
        NewValue = GetPlayerBankItemValue(Index, NewSlot)

        SetPlayerBankItemNum(Index, NewSlot, OldNum)
        SetPlayerBankItemValue(Index, NewSlot, OldValue)

        SetPlayerBankItemNum(Index, OldSlot, NewNum)
        SetPlayerBankItemValue(Index, OldSlot, NewValue)

        ReDim OldStats(Stats.Count - 1)
        ReDim NewStats(Stats.Count - 1)

        ' RandomInv
        With Bank(Index).ItemRand(NewSlot)
            NewPrefix = .Prefix
            NewSuffix = .Suffix
            NewDamage = .Damage
            NewSpeed = .Speed
            NewRarity = .Rarity
            For i = 1 To Stats.Count - 1
                NewStats(i) = .Stat(i)
            Next i
        End With

        With Bank(Index).ItemRand(OldSlot)
            OldPrefix = .Prefix
            OldSuffix = .Suffix
            OldDamage = .Damage
            OldSpeed = .Speed
            OldRarity = .Rarity
            For i = 1 To Stats.Count - 1
                OldStats(i) = .Stat(i)
            Next i
        End With

        With Bank(Index).ItemRand(NewSlot)
            .Prefix = OldPrefix
            .Suffix = OldSuffix
            .Damage = OldDamage
            .Speed = OldSpeed
            .Rarity = OldRarity
            For i = 1 To Stats.Count - 1
                .Stat(i) = OldStats(i)
            Next i
        End With

        With Bank(Index).ItemRand(OldSlot)
            .Prefix = NewPrefix
            .Suffix = NewSuffix
            .Damage = NewDamage
            .Speed = NewSpeed
            .Rarity = NewRarity
            For i = 1 To Stats.Count - 1
                .Stat(i) = NewStats(i)
            Next i
        End With

        SendBank(Index)
    End Sub
#End Region

End Module