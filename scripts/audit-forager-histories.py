"""Compare real saved history metrics; unfinished runs remain explicitly pending."""
import argparse
import json
from pathlib import Path
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

parser=argparse.ArgumentParser()
parser.add_argument("histories",nargs="+",type=Path)
parser.add_argument("--output",type=Path,required=True)
parser.add_argument("--require-complete",action="store_true")
args=parser.parse_args()
args.output.mkdir(parents=True,exist_ok=True)
fig,axes=plt.subplots(3,2,figsize=(14,12),layout="constrained")
results=[]
for historyIndex,folder in enumerate(args.histories):
    color=plt.get_cmap("tab10")(historyIndex)
    run=json.loads((folder/"run.json").read_text())
    rows=json.loads((folder/"metrics.json").read_text())
    years=[r["year"] for r in rows]
    assert years==sorted(set(years)),f"Repeated or reversed dates: {folder}"
    for r in rows:
        assert r["population"]>0 and r["births"]>=0 and r["deaths"]>=0
        assert 0<=r["sapiensAncestry"]<=r["population"]+1
        assert r["languages"]<=r["residentSpeechVarieties"]
        assert r["families"]<=r["residentFamilies"]
    complete=years[-1]==run["requestedYears"]
    declines=sum(b["population"]<a["population"]*.98 for a,b in zip(rows,rows[1:]))
    expansions=sum(b["districts"]>a["districts"] for a,b in zip(rows,rows[1:]))
    retreats=sum(b["districts"]<a["districts"] for a,b in zip(rows,rows[1:]))
    toolkits=json.loads((folder/"techniques.json").read_text())
    result=dict(name=folder.name,source=run["source"],year=years[-1],requestedYear=run["requestedYears"],status="complete history; outcome review required" if complete else "RUNNING OR INCOMPLETE; not a full-history pass",populationMin=min(r["population"] for r in rows),populationMax=max(r["population"] for r in rows),populationDeclineIntervals=declines,rangeExpansionIntervals=expansions,rangeRetreatIntervals=retreats,maximumPopulationToCapacity=max(r["population"]/r["capacity"] for r in rows),finalPeoplePerResidentCohort=rows[-1]["population"]/rows[-1]["cohorts"],skilledTechniqueCount=sum(t["skilled"]>0 for t in toolkits),wellRepresentedTechniqueCount=sum(t["skilled"]>=10 for t in toolkits),lastSnapshotBytes=max((p.stat().st_size for p in folder.glob("*.world")),default=0))
    results.append(result)
    x=[y/1000 for y in years];name=folder.name
    axes[0,0].plot(x,[r["population"]/1000 for r in rows],label=name,color=color)
    axes[0,0].plot(x,[r["capacity"]/1000 for r in rows],linestyle=":",alpha=.45,color=color)
    axes[0,1].plot(x,[r["districts"] for r in rows],label=name,color=color)
    axes[1,0].plot(x,[r["coldPopulation"]/r["population"]*100 for r in rows],label=name,color=color)
    axes[1,1].plot(x,[r["residentSpeechVarieties"] for r in rows],label=name,color=color)
    axes[1,1].plot(x,[r["residentFamilies"] for r in rows],linestyle=":",alpha=.6,color=color)
    axes[2,0].plot(x,[r["meanSkill"] for r in rows],label=name,color=color)
    axes[2,0].fill_between(x,[r["minSkill"] for r in rows],[r["maxSkill"] for r in rows],alpha=.08,color=color)
    axes[2,1].plot(x,[r["sapiensAncestry"]/r["population"]*100 for r in rows],label=name,color=color)
for ax,title,ylabel in zip(axes.flat,["Population and available support (dotted)","Occupied districts: expansion and retreat","Share living through cold winters","Resident speech varieties / families (dotted)","Mean trained repertoire; range shaded","Sapiens ancestry, including mixed populations"],["Thousands of people","Districts","Percent of people","Recorded communities","Sum of technique proficiency","Percent of ancestry"]):
    ax.set(title=title,xlabel="Thousands of simulated years after start",ylabel=ylabel)
    ax.grid(alpha=.2);ax.legend(fontsize=7)
fig.suptitle("Coupled Paleolithic histories: measured outcomes, not final acceptance",fontsize=16)
fig.savefig(args.output/"history-comparison.png",dpi=140)
(args.output/"history-comparison.json").write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
if args.require_complete and not all(x["year"]==x["requestedYear"] for x in results):
    raise SystemExit("INCOMPLETE: full requested histories have not finished.")
