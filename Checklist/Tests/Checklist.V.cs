﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace Checklist
{
    public partial class Checklist
    {
        public void V()
        {
            if (checklistType == ChecklistType.EclipseVMAT || checklistType == ChecklistType.MasterPlanIMRT)
            {
                checklistItems.Add(new ChecklistItem("V. VMAT/IMRT"));

                string v1_value = string.Empty;
                
                AutoCheckStatus v1_status = AutoCheckStatus.FAIL;
                int v1_numberOfWarnings = 0;
                int v1_numberOfPass = 0;
                List<double> collAngles = new List<double>();
                List<string> lowerPTVObjectiveStructures = new List<string>(); //J Add list for name of all structures that have an lower objective
                foreach (Beam beam in planSetup.Beams)
                {
                    if (!beam.IsSetupField)
                    {
                        double collimatorAngle = beam.ControlPoints[0].CollimatorAngle;
                        collAngles.Add(collimatorAngle);
                        if (treatmentUnitManufacturer == TreatmentUnitManufacturer.Varian)
                        { 
                            if (collimatorAngle == 5 || collimatorAngle == 355)
                                v1_numberOfPass++;
                            else if (collimatorAngle > 5 && collimatorAngle < 355)
                                v1_numberOfWarnings++;
                        }
                        else if (treatmentUnitManufacturer == TreatmentUnitManufacturer.Elekta)
                        { 
                            if (collimatorAngle == 30 || collimatorAngle == 330)
                                v1_numberOfPass++;
                        }

                        v1_value += (v1_value.Length == 0 ? string.Empty : ", ") + beam.Id + ": " + collimatorAngle.ToString("0.0") + "°";
                    }
                }
                if (v1_numberOfPass == numberOfTreatmentBeams)
                    v1_status = AutoCheckStatus.PASS;
                else if (v1_numberOfPass + v1_numberOfWarnings == numberOfTreatmentBeams)
                    v1_status = AutoCheckStatus.WARNING;
                if (collAngles.Count > 1 && collAngles.Distinct().ToList().Count < 2)
                    v1_status = AutoCheckStatus.FAIL;

                checklistItems.Add(new ChecklistItem("V1. Kollimatorvinkeln är lämplig", "Kontrollera att kollimatorvinkeln är lämplig\r\n  • Varian: vanligtvis 5° resp. 355°, men passar detta ej PTV är andra vinklar ok (dock ej vinklar mellan 355° och 5°)\r\n  • Elekta: 30° resp. 330°", v1_value, v1_status));
                                
                if (checklistType == ChecklistType.EclipseVMAT && treatmentUnitManufacturer == TreatmentUnitManufacturer.Varian)
                {
                    string v2_value = string.Empty;
                    AutoCheckStatus v2_status = AutoCheckStatus.WARNING;
                    int v2_numberOfPass = 0;
                    foreach (Beam beam in planSetup.Beams)
                    {
                        if (!beam.IsSetupField)
                        {
                            double fieldWidth = 0.1 * (beam.ControlPoints[0].JawPositions.X2 - beam.ControlPoints[0].JawPositions.X1);
                            if (fieldWidth <= 15)
                                v2_numberOfPass++;
                            v2_value += (v2_value.Length == 0 ? string.Empty : ", ") + beam.Id + ": " + fieldWidth.ToString("0.0") + " cm";
                        }
                    }
                    if (v2_numberOfPass == numberOfTreatmentBeams)
                        v2_status = AutoCheckStatus.PASS;
                    checklistItems.Add(new ChecklistItem("V2. Fältbredden är rimlig ", "Kontrollera att VMAT-fält har en rimlig fältbredd (riktvärde 15 cm, vid större target ska två arcs och delade fält övervägas).", v2_value, v2_status));

                    string v3_details = string.Empty;
                    string v3_value = string.Empty;
                    AutoCheckStatus v3_status = AutoCheckStatus.MANUAL;
                    if (planSetup.OptimizationSetup != null)
                    {
                        // JSR 
                        List<Structure> strList = structureSet.Structures.Where(s => s.Id.StartsWith("Z_PTV")).ToList(); 
                        
                        
                            foreach (OptimizationObjective optimizationObjective in planSetup.OptimizationSetup.Objectives)
                                if (optimizationObjective.GetType() == typeof(OptimizationPointObjective))
                                {
                                    OptimizationPointObjective optimizationPointObjective = (OptimizationPointObjective)optimizationObjective;
                                    if ((optimizationPointObjective.Operator.ToString().ToLower() == "lower") && optimizationPointObjective.StructureId.StartsWith("Z_PTV"))
                                    {
                                        // Generates a list for with name of all structures that have a lower objective (ie finds the PTVs). 
                                        lowerPTVObjectiveStructures.Add(optimizationPointObjective.StructureId);
                                    }
                                    v3_details += (v3_details.Length == 0 ? "Optimization objectives:\r\n  " : "\r\n  ") + optimizationPointObjective.StructureId + ": " + optimizationPointObjective.Operator.ToString() + ", dose: " + optimizationPointObjective.Dose.Dose.ToString("0.000") + ", volume: " + optimizationPointObjective.Volume.ToString("0.0") + ", priority: " + optimizationPointObjective.Priority.ToString();
                                }
                        if (!strList.Any() && !lowerPTVObjectiveStructures.Any())
                        { 
                                v3_value += "Inget optimeringsPTV hittat, verifera"; 
                                v3_status = AutoCheckStatus.MANUAL;
                        }
                        else if (strList.Any() && !lowerPTVObjectiveStructures.Any())
                        { 
                                v3_value += "OptimeringsPTV har ritats men ej används i optimering, vänligen verifera";
                                v3_status = AutoCheckStatus.WARNING;
                        }
                        else if (strList.Any() && lowerPTVObjectiveStructures.Any())
                        { 
                                v3_value += "OptimeringsPTV har ritats och använts optimering, vänligen verifiera";
                                v3_status = AutoCheckStatus.MANUAL;
                        }

                        // JSR 
                        foreach (OptimizationParameter optimizationParameter in planSetup.OptimizationSetup.Parameters)
                        {
                            if (optimizationParameter.GetType() == typeof(OptimizationPointCloudParameter))
                            {
                                OptimizationPointCloudParameter optimizationPointCloudParameter = (OptimizationPointCloudParameter)optimizationParameter;
                                v3_details += (v3_details.Length == 0 ? string.Empty : "\r\n") + "Point cloud parameter: " + optimizationPointCloudParameter.Structure.Id + "=" + optimizationPointCloudParameter.Structure.DicomType.ToString();
                            }
                            else if (optimizationParameter.GetType() == typeof(OptimizationNormalTissueParameter))
                            {
                                OptimizationNormalTissueParameter optimizationNormalTissueParameter = (OptimizationNormalTissueParameter)optimizationParameter;
                                v3_details += (v3_details.Length == 0 ? string.Empty : "\r\n") + "Normal tissue parameter: priority=" + optimizationNormalTissueParameter.Priority.ToString();
                            }
                            else if (optimizationParameter.GetType() == typeof(OptimizationExcludeStructureParameter))
                            {
                                OptimizationExcludeStructureParameter optimizationExcludeStructureParameter = (OptimizationExcludeStructureParameter)optimizationParameter;
                                v3_details += (v3_details.Length == 0 ? string.Empty : "\r\n") + "Exclude structure parameter: " + optimizationExcludeStructureParameter.Structure.Id;
                            }
                        }
                    }
                    checklistItems.Add(new ChecklistItem("V3. Optimeringsbolus och optimeringsPTV är korrekt använt", "Kontrollera att optimeringsbolus har använts korrekt för ytliga target:	\r\n  Eclipse H&N (VMAT):\r\n    • Optimerings-PTV har använts vid optimeringen i de fall då PTV har beskurits med hänsyn till ytterkonturen\r\n    • HELP_BODY inkluderar både patientens ytterkontur (BODY) och optimeringsbolus\r\n  Eclipse Ani, Recti (VMAT):\r\n    • BODY ska inkludera eventuellt optimeringsbolus\r\n  Optimeringsbolus i Eclipse (VMAT):\r\n    • HU för optimeringsbolus är satt till 0 HU\r\n    • Optimeringsbolus är skapat genom 5 mm (H&N) eller 6 mm (Ani, Recti) expansion från det PTV-struktur optimeringen skett på. Boluset ska ej gå innanför patientens hudyta.", v3_value, v3_details, v3_status)); //JSR

                    checklistItems.Add(new ChecklistItem("V4. Robusthet", "Kontrollera planens robusthet m.a.p. ISO-center-förskjutning m.h.a. Uncertainty-planer. Planerna skapas av dosplaneraren.\r\n    • Skillnaderna i maxdos för uncertainty-planerna (±0,4 cm i x, y, resp. z) är <5% relativt originalplanen.\r\n    • CTV täckning är acceptabel.", string.Empty, AutoCheckStatus.MANUAL));

                    checklistItems.Add(new ChecklistItem("V5. Leveransmönstret är rimligt", "Kontrollera att leveransmönstret är rimligt (att det inte är en stor andel extremt små öppningar och att riskorgan skärmas, samt att alla segment går på ett target)", string.Empty, AutoCheckStatus.MANUAL));
                }
            }
        }
    }
}
