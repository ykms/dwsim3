﻿'    Seawater Property Package 
'    Copyright 2015 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.


Imports DWSIM.DWSIM.SimulationObjects.PropertyPackages
Imports DWSIM.DWSIM.SimulationObjects.PropertyPackages.Auxiliary
Imports DWSIM.DWSIM.MathEx
Imports System.Linq
Imports DWSIM.DWSIM.ClassesBasicasTermodinamica

Namespace DWSIM.SimulationObjects.PropertyPackages

    <System.Runtime.InteropServices.Guid(SteamTablesPropertyPackage.ClassId)> _
<System.Serializable()> Public Class SeawaterPropertyPackage

        Inherits DWSIM.SimulationObjects.PropertyPackages.PropertyPackage

        Public Shadows Const ClassId As String = "170D6E8A-8880-4bf9-B7A0-E4A3FDBFD145"

        Protected m_iapws97 As New IAPWS_IF97

        Protected SIA As New Seawater

        Public Sub New(ByVal comode As Boolean)

            MyBase.New(comode)

        End Sub

        Public Sub New()

            MyBase.New()

            Me.SupportedComponents.Add(15)

            Me.IsConfigurable = True
            Me.ConfigForm = New FormConfigPP

            Me._packagetype = PropertyPackages.PackageType.Miscelaneous

        End Sub

        Public Overrides Sub ConfigParameters()
            m_par = New System.Collections.Generic.Dictionary(Of String, Double)
            With Me.Parameters
                .Clear()
                .Add("PP_PHFILT", 0.001)
                .Add("PP_PSFILT", 0.001)
                .Add("PP_PHFELT", 0.001)
                .Add("PP_PSFELT", 0.001)
                .Add("PP_PHFMEI", 50)
                .Add("PP_PSFMEI", 50)
                .Add("PP_PHFMII", 100)
                .Add("PP_PSFMII", 100)
                .Add("PP_PTFMEI", 100)
                .Add("PP_PTFMII", 100)
                .Add("PP_PTFILT", 0.001)
                .Add("PP_PTFELT", 0.001)
                .Add("PP_IGNORE_SALINITY_LIMIT", 0)
            End With
        End Sub

        Public Overrides Sub ReconfigureConfigForm()
            MyBase.ReconfigureConfigForm()
            Me.ConfigForm = New FormConfigPP
        End Sub

        Public Overrides ReadOnly Property FlashBase() As Auxiliary.FlashAlgorithms.FlashAlgorithm
            Get
                Dim constprops As New List(Of ConstantProperties)
                For Each su As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values
                    constprops.Add(su.ConstantProperties)
                Next
                Return New Auxiliary.FlashAlgorithms.Seawater With {.CompoundProperties = constprops}
            End Get
        End Property

        Public Overrides Function AUX_VAPDENS(ByVal T As Double, ByVal P As Double) As Double

            Return Me.SIA.vap_density_si(T, P)

        End Function

        Public Overrides Sub DW_CalcProp(ByVal [property] As String, ByVal phase As Fase)

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water Is Nothing Then Throw New Exception("Water compound not found. Please setup your simulation accordingly.")
            If salt Is Nothing Then Throw New Exception("Salt compound not found. Please setup your simulation accordingly.")

            Dim result As Double = 0.0#
            Dim resultObj As Object = Nothing
            Dim phaseID As Integer = -1
            Dim state As String = ""

            Dim T, P As Double
            T = Me.CurrentMaterialStream.Fases(0).SPMProperties.temperature.GetValueOrDefault
            P = Me.CurrentMaterialStream.Fases(0).SPMProperties.pressure.GetValueOrDefault

            Select Case phase
                Case Fase.Vapor
                    state = "V"
                Case Fase.Liquid, Fase.Liquid1, Fase.Liquid2, Fase.Liquid3, Fase.Aqueous
                    state = "L"
                Case Fase.Solid
                    state = "S"
            End Select

            Select Case phase
                Case PropertyPackages.Fase.Mixture
                    phaseID = 0
                Case PropertyPackages.Fase.Vapor
                    phaseID = 2
                Case PropertyPackages.Fase.Liquid1
                    phaseID = 3
                Case PropertyPackages.Fase.Liquid2
                    phaseID = 4
                Case PropertyPackages.Fase.Liquid3
                    phaseID = 5
                Case PropertyPackages.Fase.Liquid
                    phaseID = 1
                Case PropertyPackages.Fase.Aqueous
                    phaseID = 6
                Case PropertyPackages.Fase.Solid
                    phaseID = 7
            End Select

            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight = Me.AUX_MMM(phase)

            Select Case phase
                Case Fase.Vapor

                    Select Case [property].ToLower
                        Case "compressibilityfactor"
                            result = 1 / (Me.m_iapws97.densW(T, P / 100000) * 1000 / 18) / 8.314 / T * P
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.compressibilityFactor = result
                        Case "heatcapacity", "heatcapacitycp"
                            result = Me.m_iapws97.cpW(T, P / 100000) '* 18
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCp = result
                        Case "heatcapacitycv"
                            result = Me.m_iapws97.cvW(T, P / 100000) '* 18
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCv = result
                        Case "enthalpy", "enthalpynf"
                            result = Me.DW_CalcEnthalpy(RET_VMOL(Fase.Vapor), T, P, PropertyPackages.State.Vapor)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy = result
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpy = result
                        Case "entropy", "entropynf"
                            result = Me.DW_CalcEntropy(RET_VMOL(Fase.Vapor), T, P, PropertyPackages.State.Vapor)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy = result
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropy = result
                        Case "excessenthalpy"
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.excessEnthalpy = 0.0#
                        Case "excessentropy"
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.excessEntropy = 0.0#
                        Case "enthalpyf"
                            Dim entF As Double = Me.AUX_HFm25(phase)
                            result = Me.DW_CalcEnthalpy(RET_VMOL(Fase.Vapor), T, P, PropertyPackages.State.Vapor)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpyF = result + entF
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpyF.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpyF = result
                        Case "entropyf"
                            Dim entF As Double = Me.AUX_SFm25(phase)
                            result = Me.DW_CalcEntropy(RET_VMOL(Fase.Vapor), T, P, PropertyPackages.State.Vapor)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropyF = result + entF
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropyF.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropyF = result
                        Case "viscosity"
                            result = Me.m_iapws97.viscW(T, P / 100000)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.viscosity = result
                        Case "thermalconductivity"
                            result = Me.m_iapws97.thconW(T, P / 100000)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.thermalConductivity = result
                        Case "fugacity", "fugacitycoefficient", "logfugacitycoefficient", "activity", "activitycoefficient"
                            Me.DW_CalcCompFugCoeff(phase)
                        Case "volume", "density"
                            result = Me.m_iapws97.densW(T, P / 100000)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.density = result
                        Case "surfacetension"
                            Me.CurrentMaterialStream.Fases(0).TPMProperties.surfaceTension = Me.AUX_SURFTM(T)
                        Case Else
                            Dim ex As Exception = New CapeOpen.CapeThrmPropertyNotAvailableException
                            ThrowCAPEException(ex, "Error", ex.Message, "ICapeThermoMaterial", ex.Source, ex.StackTrace, "CalcSinglePhaseProp/CalcTwoPhaseProp/CalcProp", ex.GetHashCode)
                    End Select

                Case Fase.Liquid1

                    Dim salinity As Double = CalcSalinity()

                    Select Case [property].ToLower
                        Case "compressibilityfactor"
                            result = 1 / (Me.SIA.sea_density_si(salinity, T, P) * 1000 / 18) / 8.314 / T * P
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.compressibilityFactor = result
                        Case "heatcapacity", "heatcapacitycp"
                            result = Me.SIA.sea_cp_si(salinity, T, P) / 1000
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCp = result
                        Case "heatcapacitycv"
                            result = Me.SIA.sea_cp_si(salinity, T, P) / 1000
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCv = result
                        Case "enthalpy", "enthalpynf"
                            result = Me.DW_CalcEnthalpy(RET_VMOL(Fase.Liquid1), T, P, PropertyPackages.State.Liquid)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy = result
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpy = result
                        Case "entropy", "entropynf"
                            result = Me.DW_CalcEntropy(RET_VMOL(Fase.Liquid1), T, P, PropertyPackages.State.Liquid)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy = result
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropy = result
                        Case "excessenthalpy"
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.excessEnthalpy = 0.0#
                        Case "excessentropy"
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.excessEntropy = 0.0#
                        Case "enthalpyf"
                            Dim entF As Double = Me.AUX_HFm25(phase)
                            result = Me.m_iapws97.enthalpyW(T, P / 100000)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpyF = result + entF
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpyF.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpyF = result
                        Case "entropyf"
                            Dim entF As Double = Me.AUX_SFm25(phase)
                            result = Me.m_iapws97.entropyW(T, P / 100000)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropyF = result + entF
                            result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropyF.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropyF = result
                        Case "viscosity"
                            result = Me.SIA.sea_viscosity(salinity, T)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.viscosity = result
                        Case "thermalconductivity"
                            result = Me.SIA.sea_thermalcond(salinity, T)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.thermalConductivity = result
                        Case "fugacity", "fugacitycoefficient", "logfugacitycoefficient", "activity", "activitycoefficient"
                            Me.DW_CalcCompFugCoeff(phase)
                        Case "volume", "density"
                            result = Me.SIA.sea_density_si(salinity, T, P)
                            Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.density = result
                        Case "surfacetension"
                            Me.CurrentMaterialStream.Fases(0).TPMProperties.surfaceTension = Me.AUX_SURFTM(T)
                        Case Else
                            Dim ex As Exception = New CapeOpen.CapeThrmPropertyNotAvailableException
                            ThrowCAPEException(ex, "Error", ex.Message, "ICapeThermoMaterial", ex.Source, ex.StackTrace, "CalcSinglePhaseProp/CalcTwoPhaseProp/CalcProp", ex.GetHashCode)
                    End Select

            End Select


        End Sub

        Public Overrides Sub DW_CalcPhaseProps(ByVal fase As DWSIM.SimulationObjects.PropertyPackages.Fase)

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water Is Nothing Then Throw New Exception("Water compound not found. Please setup your simulation accordingly.")
            If salt Is Nothing Then Throw New Exception("Salt compound not found. Please setup your simulation accordingly.")

            Dim result As Double

            Dim T, P As Double
            Dim composition As Object = Nothing
            Dim phasemolarfrac As Double = Nothing
            Dim overallmolarflow As Double = Nothing

            Dim phaseID As Integer
            T = Me.CurrentMaterialStream.Fases(0).SPMProperties.temperature.GetValueOrDefault
            P = Me.CurrentMaterialStream.Fases(0).SPMProperties.pressure.GetValueOrDefault

            Select Case fase
                Case PropertyPackages.Fase.Mixture
                    phaseID = 0
                Case PropertyPackages.Fase.Vapor
                    phaseID = 2
                Case PropertyPackages.Fase.Liquid1
                    phaseID = 3
                Case PropertyPackages.Fase.Liquid2
                    phaseID = 4
                Case PropertyPackages.Fase.Liquid3
                    phaseID = 5
                Case PropertyPackages.Fase.Liquid
                    phaseID = 1
                Case PropertyPackages.Fase.Aqueous
                    phaseID = 6
                Case PropertyPackages.Fase.Solid
                    phaseID = 7
            End Select

            If phaseID > 0 Then

                overallmolarflow = Me.CurrentMaterialStream.Fases(0).SPMProperties.molarflow.GetValueOrDefault
                phasemolarfrac = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molarfraction.GetValueOrDefault
                result = overallmolarflow * phasemolarfrac
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molarflow = result
                result = result * Me.AUX_MMM(fase) / 1000
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.massflow = result
                If Me.CurrentMaterialStream.Fases(0).SPMProperties.massflow.GetValueOrDefault > 0 Then
                    result = phasemolarfrac * overallmolarflow * Me.AUX_MMM(fase) / 1000 / Me.CurrentMaterialStream.Fases(0).SPMProperties.massflow.GetValueOrDefault
                Else
                    result = 0
                End If
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.massfraction = result
                Me.DW_CalcCompVolFlow(phaseID)
                Me.DW_CalcCompFugCoeff(fase)

            End If

            Dim Tsat As Double = Me.m_iapws97.tSatW(P / 100000)

            If phaseID = 2 Then

                result = Me.m_iapws97.densW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.density = result
                result = Me.m_iapws97.enthalpyW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy = result
                result = Me.m_iapws97.entropyW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy = result
                result = 1 / (Me.m_iapws97.densW(T, P / 100000) * 1000 / 18) / 8.314 / T * P
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.compressibilityFactor = result
                result = Me.m_iapws97.cpW(T, P / 100000) '* 18
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCp = result
                result = Me.m_iapws97.cvW(T, P / 100000) '* 18
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCv = result
                result = 18
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight = result
                result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpy = result
                result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropy = result
                result = Me.m_iapws97.thconW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.thermalConductivity = result
                result = Me.m_iapws97.viscW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.viscosity = result
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.kinematic_viscosity = result / Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.density.Value

            ElseIf phaseID = 3 Then

                Dim salinity As Double = CalcSalinity()

                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight = Me.AUX_MMM(PropertyPackages.Fase.Liquid1)
                result = 1 / (Me.SIA.sea_density_si(salinity, T, P) * 1000 / 18) / 8.314 / T * P
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.compressibilityFactor = result
                result = Me.SIA.sea_cp_si(salinity, T, P) / 1000
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCp = result
                result = Me.SIA.sea_cp_si(salinity, T, P) / 1000
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.heatCapacityCv = result
                result = Me.SIA.sea_enthalpy_si(salinity, T, P) / 1000
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy = result
                result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpy = result
                result = Me.SIA.sea_entropy_si(salinity, T, P) / 1000
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy = result
                result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropy.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropy = result
                Dim entF As Double = Me.AUX_HFm25(PropertyPackages.Fase.Liquid1)
                result = Me.m_iapws97.enthalpyW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpyF = result + entF
                result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.enthalpyF.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_enthalpyF = result
                entF = Me.AUX_SFm25(PropertyPackages.Fase.Liquid1)
                result = Me.m_iapws97.entropyW(T, P / 100000)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropyF = result + entF
                result = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.entropyF.GetValueOrDefault * Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molecularWeight.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.molar_entropyF = result
                result = Me.SIA.sea_viscosity(salinity, T)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.viscosity = result
                result = Me.SIA.sea_thermalcond(salinity, T)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.thermalConductivity = result
                Me.DW_CalcCompFugCoeff(PropertyPackages.Fase.Liquid1)
                result = Me.SIA.sea_density_si(salinity, T, P)
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.density = result
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.kinematic_viscosity = Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.viscosity.GetValueOrDefault / result
                Me.CurrentMaterialStream.Fases(0).TPMProperties.surfaceTension = Me.AUX_SURFTM(T)

            ElseIf phaseID = 1 Then

                DW_CalcLiqMixtureProps()

            Else

                DW_CalcOverallProps()

            End If

            If phaseID > 0 Then
                result = overallmolarflow * phasemolarfrac * Me.AUX_MMM(fase) / 1000 / Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.density.GetValueOrDefault
                Me.CurrentMaterialStream.Fases(phaseID).SPMProperties.volumetric_flow = result
            End If

        End Sub

        Public Overrides Sub DW_CalcTwoPhaseProps(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal fase2 As DWSIM.SimulationObjects.PropertyPackages.Fase)

            Dim result As Double

            Dim T, P As Double
            Dim composition1 As Object = Nothing
            Dim composition2 As Object = Nothing

            T = Me.CurrentMaterialStream.Fases(0).SPMProperties.temperature.GetValueOrDefault
            P = Me.CurrentMaterialStream.Fases(0).SPMProperties.pressure.GetValueOrDefault

            result = 1
            Me.CurrentMaterialStream.Fases(0).TPMProperties.kvalue = result
            result = 0
            Me.CurrentMaterialStream.Fases(0).TPMProperties.logKvalue = result

            Me.CurrentMaterialStream.Fases(0).TPMProperties.surfaceTension = DW_CalcTensaoSuperficial_ISOL(Fase.Liquid1, T, P)

        End Sub

        Public Overrides Function DW_CalcMassaEspecifica_ISOL(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal T As Double, ByVal P As Double, Optional ByVal pvp As Double = 0) As Double
            If fase1 = Fase.Liquid Then
                Return Me.m_iapws97.densW(T, P / 100000)
            ElseIf fase1 = Fase.Vapor Then
                If Me.m_iapws97.pSatW(T) / 100000 = P Then
                    Return Me.m_iapws97.densSatVapTW(T)
                Else
                    Return Me.m_iapws97.densW(T, P / 100000)
                End If
            ElseIf fase1 = Fase.Mixture Then
                Return Me.m_iapws97.densW(T, P / 100000)
            End If
        End Function

        Public Overrides Function DW_CalcTensaoSuperficial_ISOL(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal T As Double, ByVal P As Double) As Double
            Return SIA.sea_surfacetension(CalcSalinity, T)
        End Function

        Public Overrides Function DW_CalcViscosidadeDinamica_ISOL(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal T As Double, ByVal P As Double) As Double

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water Is Nothing Then Throw New Exception("Water compound not found. Please setup your simulation accordingly.")
            If salt Is Nothing Then Throw New Exception("Salt compound not found. Please setup your simulation accordingly.")

            Dim salinity As Double = salt.FracaoMassica.GetValueOrDefault / water.FracaoMassica.GetValueOrDefault

            If fase1 = Fase.Liquid Then
                Return Me.SIA.sea_viscosity(salinity, T)
            ElseIf fase1 = Fase.Vapor Then
                If Me.m_iapws97.pSatW(T) / 100000 = P Then
                    Return Me.m_iapws97.viscSatVapTW(T)
                Else
                    Return Me.m_iapws97.viscW(T, P / 100000)
                End If
            End If
        End Function

        Public Overrides Function DW_CalcEnergiaMistura_ISOL(ByVal T As Double, ByVal P As Double) As Double
            Dim ent_massica = Me.m_iapws97.enthalpyW(T, P / 100000)
            Dim flow = Me.CurrentMaterialStream.Fases(0).SPMProperties.massflow
            Return ent_massica * flow
        End Function

        Public Overrides Function DW_CalcCp_ISOL(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal T As Double, ByVal P As Double) As Double
            Return Me.SIA.sea_cp_si(CalcSalinity, T, P)
        End Function

        Public Overrides Function DW_CalcK_ISOL(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal T As Double, ByVal P As Double) As Double
            If fase1 = Fase.Liquid Then
                Return Me.m_iapws97.thconW(T, P / 100000)
            ElseIf fase1 = Fase.Vapor Then
                If Me.m_iapws97.pSatW(T) / 100000 = P Then
                    Return Me.m_iapws97.thconSatVapTW(T)
                Else
                    Return Me.m_iapws97.thconW(T, P / 100000)
                End If
            End If
        End Function

        Public Overrides Function DW_CalcMM_ISOL(ByVal fase1 As DWSIM.SimulationObjects.PropertyPackages.Fase, ByVal T As Double, ByVal P As Double) As Double
            Return 18
        End Function

        Public Overrides Function DW_CalcPVAP_ISOL(ByVal T As Double) As Double

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water Is Nothing Then Throw New Exception("Water compound not found. Please setup your simulation accordingly.")
            If salt Is Nothing Then Throw New Exception("Salt compound not found. Please setup your simulation accordingly.")

            Dim salinity As Double = salt.FracaoMassica.GetValueOrDefault / water.FracaoMassica.GetValueOrDefault

            Return Me.SIA.sea_vaporpressure(salinity, T)

        End Function

        Public Overrides Function SupportsComponent(ByVal comp As ClassesBasicasTermodinamica.ConstantProperties) As Boolean

            If Me.SupportedComponents.Contains(comp.ID) Then
                Return True
            Else
                Return False
            End If

        End Function

        Public Overrides Function DW_CalcEnthalpy(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double

            If DirectCast(Vx, Double()).Sum > 0.0# Then
                Select Case st
                    Case State.Liquid
                        Return Me.SIA.sea_enthalpy_si(CalcSalinity(Vx), T, P) / 1000
                    Case State.Vapor
                        Return Me.SIA.sea_enthalpy_si(CalcSalinity(Vx), T, P) / 1000 + Me.RET_HVAPM(AUX_CONVERT_MOL_TO_MASS(Vx), T)
                End Select
            Else
                Return 0.0#
            End If

        End Function

        Public Overrides Function DW_CalcKvalue(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double) As Double()
            Return New Double() {1.0#, 1.0#}
        End Function

        Public Overrides Function DW_CalcEnthalpyDeparture(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double
            Return Me.DW_CalcEnthalpy(Vx, T, P, st) - Me.RET_Hid(298.15, T, Vx)
        End Function

        Public Overrides Function DW_CalcBubP(ByVal Vx As System.Array, ByVal T As Double, Optional ByVal Pref As Double = 0, Optional ByVal K As System.Array = Nothing, Optional ByVal ReuseK As Boolean = False) As Object
            Return New Object() {Me.SIA.sea_vaporpressure(CalcSalinity(Vx), T)}
        End Function

        Public Overrides Function DW_CalcBubT(ByVal Vx As System.Array, ByVal P As Double, Optional ByVal Tref As Double = 0, Optional ByVal K As System.Array = Nothing, Optional ByVal ReuseK As Boolean = False) As Object
            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Return New Object() {Me.AUX_TSATi(P, water.Nome)}
        End Function

        Public Overrides Function DW_CalcDewP(ByVal Vx As System.Array, ByVal T As Double, Optional ByVal Pref As Double = 0, Optional ByVal K As System.Array = Nothing, Optional ByVal ReuseK As Boolean = False) As Object
            Return New Object() {Me.SIA.sea_vaporpressure(CalcSalinity(Vx), T)}
        End Function

        Public Overrides Function DW_CalcDewT(ByVal Vx As System.Array, ByVal P As Double, Optional ByVal Tref As Double = 0, Optional ByVal K As System.Array = Nothing, Optional ByVal ReuseK As Boolean = False) As Object
            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Return New Object() {Me.AUX_TSATi(P, water.Nome)}
        End Function

        Public Overrides Function DW_CalcCv_ISOL(ByVal fase1 As Fase, ByVal T As Double, ByVal P As Double) As Double
            Return Me.SIA.sea_cp_si(CalcSalinity, T, P)
        End Function

        Public Overrides Sub DW_CalcCompPartialVolume(ByVal phase As Fase, ByVal T As Double, ByVal P As Double)

        End Sub

        Public Overrides Function DW_CalcEntropy(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double
            If DirectCast(Vx, Double()).Sum > 0.0# Then
                Select Case st
                    Case State.Liquid
                        Return Me.SIA.sea_entropy_si(CalcSalinity, T, P) / 1000
                    Case State.Vapor
                        Return Me.SIA.sea_entropy_si(CalcSalinity(Vx), T, P) / 1000 + Me.RET_HVAPM(AUX_CONVERT_MOL_TO_MASS(Vx), T) / T
                End Select
            Else
                Return 0.0#
            End If
      End Function

        Public Overrides Function DW_CalcEntropyDeparture(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double

            Return DW_CalcEntropy(Vx, T, P, st) - Me.RET_Sid(298.15, T, P, Vx)

        End Function

        Public Overrides Function DW_CalcFugCoeff(ByVal Vx As System.Array, ByVal T As Double, ByVal P As Double, ByVal st As State) As Double()

            DWSIM.App.WriteToConsole(Me.ComponentName & " fugacity coefficient calculation for phase '" & st.ToString & "' requested at T = " & T & " K and P = " & P & " Pa.", 2)
            DWSIM.App.WriteToConsole("Compounds: " & Me.RET_VNAMES.ToArrayString, 2)
            DWSIM.App.WriteToConsole("Mole fractions: " & Vx.ToArrayString(), 2)

            Dim constprops As New List(Of ConstantProperties)
            For Each s As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values
                constprops.Add(s.ConstantProperties)
            Next

            Dim n As Integer = UBound(Vx)
            Dim i, j As Integer
            Dim fugcoeff(n) As Double

            If st = State.Liquid Then

                Dim Tc As Object = Me.RET_VTC()

                For i = 0 To n

                    If Tc(i) = 0.0# Then

                        Dim wtotal As Double = 0
                        Dim mtotal As Double = 0
                        Dim molality(n) As Double

                        For j = 0 To n
                            If constprops(j).Name = "Water" Then
                                wtotal += Vx(j) * constprops(j).Molar_Weight / 1000
                            End If
                            mtotal += Vx(j)
                        Next

                        Dim Xsolv As Double = 1

                        For j = 0 To n
                            molality(i) = Vx(i) / wtotal
                        Next

                        fugcoeff(i) = molality(i) * 0.665 'salt activity coefficient

                    ElseIf T / Tc(i) >= 1 Then
                        fugcoeff(i) = AUX_KHenry(Me.RET_VNAMES(i), T) / P
                    Else
                        fugcoeff(i) = Me.AUX_PVAPi(i, T) / P
                    End If

                Next

            Else
                For i = 0 To n
                    fugcoeff(i) = 1.0#
                Next
            End If

            DWSIM.App.WriteToConsole("Result: " & fugcoeff.ToArrayString(), 2)

            Return fugcoeff

        End Function

        Public Overrides Function AUX_PVAPi(sub1 As String, T As Double) As Object

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water.Nome = sub1 Then

                Return Me.SIA.sea_vaporpressure(CalcSalinity, T)

            ElseIf salt.Nome = sub1 Then

                Return 0.0#

            Else

                Return MyBase.AUX_PVAPi(sub1, T)

            End If

        End Function

        Public Function VaporPressure(Vx As Double(), T As Double) As Double

            If Vx.Sum > 0.0# Then
                Return Me.SIA.sea_vaporpressure(CalcSalinity(Vx), T)
            Else
                Return Me.m_iapws97.pSatW(T) * 100000
            End If

        End Function

        Public Function CalcSalinity(Vx As Double()) As Double

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water Is Nothing Then Throw New Exception("Water compound not found. Please setup your simulation accordingly.")
            If salt Is Nothing Then Throw New Exception("Salt compound not found. Please setup your simulation accordingly.")

            Dim idw As Integer = 0
            Dim ids As Integer = 0

            Dim i As Integer = 0
            For Each s As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values
                If s.Nome = water.Nome Then idw = i
                If s.Nome = salt.Nome Then ids = i
                i += 1
            Next

            Dim vxw As Double() = Me.AUX_CONVERT_MOL_TO_MASS(Vx)

            Dim salinity As Double = vxw(ids) / vxw(idw)

            If Double.IsInfinity(salinity) Then salinity = 0.0#

            If Parameters("PP_IGNORE_SALINITY_LIMIT") = 0 Then
                If salinity > Seawater.sal_smax Then
                    If Me.CurrentMaterialStream.FlowSheet IsNot Nothing Then
                        Me.CurrentMaterialStream.FlowSheet.WriteToLog(Me.ComponentName & "/" & New StackFrame(1).GetMethod.Name & "(): maximum salinity exceeded (" & Format(salinity, "0.00") & " kg/kg). Using upper limit value (" & Format(Seawater.sal_smax, "0.00") & " kg/kg).", Color.DarkOrange, FormClasses.TipoAviso.Aviso)
                    End If
                    salinity = Seawater.sal_smax
                End If
            End If


            Return salinity

        End Function

        Public Function CalcSalinity() As Double

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault
            Dim salt As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.Name = "Salt").SingleOrDefault

            If water Is Nothing Then Throw New Exception("Water compound not found. Please setup your simulation accordingly.")
            If salt Is Nothing Then Throw New Exception("Salt compound not found. Please setup your simulation accordingly.")

            Dim salinity As Double = salt.FracaoMassica.GetValueOrDefault / water.FracaoMassica.GetValueOrDefault

            If Double.IsInfinity(salinity) Then salinity = 0.0#

            If Parameters("PP_IGNORE_SALINITY_LIMIT") = 0 Then
                If salinity > Seawater.sal_smax Then
                    If Me.CurrentMaterialStream.FlowSheet IsNot Nothing Then
                        Me.CurrentMaterialStream.FlowSheet.WriteToLog(Me.ComponentName & "/" & New StackFrame(1).GetMethod.Name & "(): maximum salinity exceeded (" & Format(salinity, "0.00") & " kg/kg). Using upper limit value (" & Format(Seawater.sal_smax, "0.00") & " kg/kg).", Color.DarkOrange, FormClasses.TipoAviso.Aviso)
                    End If
                    salinity = Seawater.sal_smax
                End If
            End If

            Return salinity

        End Function

        Function TemperatureOfFusion(Vxl As Double(), T As Double) As Double

            Dim Tnfp, DHm, DT, Td As Double

            Dim water As Substancia = (From subst As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values Select subst Where subst.ConstantProperties.CAS_Number = "7732-18-5").SingleOrDefault

            Tnfp = 273.15
            DHm = 6.00174

            Dim idw As Integer = 0
            
            Dim i As Integer = 0
            For Each s As Substancia In Me.CurrentMaterialStream.Fases(0).Componentes.Values
                If s.Nome = water.Nome Then idw = i
                i += 1
            Next

            DT = 0.00831447 * Tnfp ^ 2 / DHm * Math.Log(Vxl(idw))
            Td = Tnfp - DT

            Return Td

        End Function

    End Class

End Namespace