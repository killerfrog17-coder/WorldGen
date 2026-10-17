using WorldGen.Core;
public static class HistoryContactCommunityRecognitionTests
{
    public static void Run(Action<bool,string> check)
    {
        bool Qualified(int revision=28,int teacher=14,int count=15,int exposure=1000,int receipt=157674,int year=157700,long residents=11044,bool storage=true,double civic=1000)
            =>HistoricalContactCommunityRecognition.Qualified(revision,teacher,count,exposure,receipt,year,residents,storage,civic);
        for(int revision=0;revision<=27;revision++)
        {
            check(!Qualified(revision:revision),"Old rule markers never activate contacted-community recognition.");
            foreach(double eco in new[]{0,.015,.021797329,.026893542,.5,1})foreach(bool qualified in new[]{false,true})
                check(HistoricalContactCommunityRecognition.EcologicalFactor(revision,eco,qualified)==.02+.98*eco,"Revisions0–27 keep the exact literal ecological diffusion factor.");
        }
        check(Qualified(),"The recorded India counters satisfy the narrow contact gates when actual delivered Storage is confirmed.");
        check(Qualified(receipt:157698,residents:1122),"The recorded Indus counters satisfy the same gates, without inventing more inhabitants.");
        check(!Qualified(teacher:-1)&&!Qualified(teacher:15)&&!Qualified(count:0),"A missing or invalid remembered teacher cannot qualify.");
        check(!Qualified(exposure:599)&&Qualified(exposure:600),"Substantial accumulated exposure is required.");
        check(!Qualified(receipt:0)&&!Qualified(receipt:-1)&&!Qualified(receipt:157701),"Undated and future receipts do not prove physical contact.");
        check(Qualified(receipt:157450)&&!Qualified(receipt:157449),"Receipt age250 qualifies; age251 is stale.");
        check(!Qualified(residents:399)&&Qualified(residents:400)&&!Qualified(residents:0),"Only the actual raw population passes the existing recognition headcount.");
        check(!Qualified(storage:false),"A teacher ID without delivered regional Storage knowledge is insufficient.");
        check(!Qualified(civic:599)&&Qualified(civic:600)&&!Qualified(civic:0),"A strong local delivered civic example remains necessary.");
        check(!Qualified(residents:35)&&!Qualified(residents:1)&&!Qualified(residents:94),"Actual Levant/Iraq/Iran raw counts remain ineligible despite large apportioned contact estimates.");
        foreach(double eco in new[]{0,.015,.021797329,.5,1})
        {
            check(HistoricalContactCommunityRecognition.EcologicalFactor(28,eco,false)==.02+.98*eco,"All unqualified revision28 cases keep the old factor.");
            check(HistoricalContactCommunityRecognition.EcologicalFactor(28,eco,true)==1,"Only qualified contact bypasses the static wild-surplus factor.");
        }
        // Recorded AA26 final source153e69... year157700. These are current
        // probability counterfactuals, not a reconstructed historical rejection.
        foreach(var sample in new[]{
            (Eco:.021797329,Environment:.013980199,Management:.5656,Concentration:.6345528455284553,Dissemination:3.572,Independent:4.928511172981426e-8,Maturity:.978823095,Old:.0018083369017483086,ExpectedNew:.04281527164556698),
            (Eco:.026893542,Environment:.014556177,Management:.5656,Concentration:.6641025641025641,Dissemination:3.572,Independent:1.618929297105667e-7,Maturity:.982097742,Old:.0021286229101745713,ExpectedNew:.044924643684516985)})
        {
            double adoption=HistoricalEnvironmentalEffects.CulturalAdoption(sample.Environment,sample.Management);
            double Probability(int revision,bool qualified,double elapsed)
            {
                double diffusion=.03*HistoricalContactCommunityRecognition.EcologicalFactor(revision,sample.Eco,qualified)*sample.Concentration;
                diffusion*=sample.Dissemination;diffusion*=adoption;
                return 1-Math.Exp(-(sample.Independent+diffusion)*sample.Maturity*elapsed/100.0);
            }
            check(Math.Abs(Probability(27,true,100)-sample.Old)<1e-12,"The current saved-world decomposition reconstructs its preceding literal probability.");
            check(Math.Abs(Probability(28,true,100)-sample.ExpectedNew)<1e-10,"Qualified current India/Indus change only the static ecological diffusion multiplier.");
            check(Probability(28,false,100)==Probability(27,false,100),"Unqualified cases preserve exact probability arithmetic.");
            check(Probability(28,true,0)==0,"No elapsed actor time creates no recognition opportunity.");
            check(adoption<.66&&adoption>.65,"The actual environmental adaptation discount is retained, not waived.");
        }
        check(HistoricalEnvironmentalEffects.CulturalAdoption(0,0)==.000001,"Unsuitable unmanaged environments retain their preceding adaptation floor.");
        check(HistoricalContactCommunityRecognition.EcologicalFactor(28,1,true)==HistoricalContactCommunityRecognition.EcologicalFactor(27,1,true),"An already maximal ecological factor receives no extra multiplier.");
    }
}
