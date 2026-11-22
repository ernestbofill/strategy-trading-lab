"""The replay behind docs/dip-trend-rule-study.md: Winners at a discount's trend rule against looser variants.

Usage: python3 dip-trend-rule-study.py DIR, where DIR holds one Yahoo chart file per symbol (SYMBOL.json), from
https://query1.finance.yahoo.com/v8/finance/chart/SYMBOL?period1=946684800&period2=<now>&interval=1d&events=div%2Csplit
It reads the adjusted closes only. A study, not the engine: see the doc for what it leaves out.
"""
import json, sys, statistics as st, datetime as dt
from collections import defaultdict
D=sys.argv[1]
LIST=['COHR','ETN','MOD','MPWR','NVT','SU.PA','SMCI','VRT','VICR']
OTHER=['1211.HK','AAPL','CBRAIN.CO','CRM','GROW.L','MRNA','MSFT','NOW','PLTR','SNOW','TSLA','ZETA','ZS']
def load(s):
    r=json.load(open(f'{D}/{s}.json'))['chart']['result'][0]
    ts=r['timestamp']; adj=r['indicators']['adjclose'][0]['adjclose']
    out=[(dt.datetime.fromtimestamp(t, dt.timezone.utc).date(),a) for t,a in zip(ts,adj) if a]
    return [d for d,_ in out],[a for _,a in out]
def sma(c,n):
    o=[None]*len(c); s=0
    for i,v in enumerate(c):
        s+=v
        if i>=n: s-=c[i-n]
        if i>=n-1: o[i]=s/n
    return o
VAR={
 'A now: 50>150>200, 200 rising': lambda p,s50,s150,s200,r: s50>s150>s200 and r,
 'B 50>200, 200 rising':          lambda p,s50,s150,s200,r: s50>s200 and r,
 'E 150>200, 200 rising':         lambda p,s50,s150,s200,r: s150>s200 and r,
 'D price>200, 200 rising':       lambda p,s50,s150,s200,r: p>s200 and r,
 'G price>150>200, 200 rising':   lambda p,s50,s150,s200,r: p>s150>s200 and r,
 'F no trend rule (growth only)': lambda p,s50,s150,s200,r: True,
}
H=[63,126,252]
def run(s):
    d,c=load(s); n=len(c)
    s50,s150,s200=sma(c,50),sma(c,150),sma(c,200)
    res={k:dict(cand=0,sigdays=0,events=[]) for k in VAR}; days=0; today={}
    last={k:-10**9 for k in VAR}; prev={k:False for k in VAR}
    for i in range(220,n):
        days+=1
        w=c[i-59:i+1]; peak=max(w); pk=i-59+max(j for j,v in enumerate(w) if v==peak)
        low=min(c[i-167:pk+1]); gain=(peak/low-1)*100>=50
        rising=s200[i]>s200[i-20]
        zh=peak*0.95; zl=max(peak*0.85,s50[i]); inzone=zl<=c[i]<=zh and zl<zh
        for k,f in VAR.items():
            cand=gain and f(c[i],s50[i],s150[i],s200[i],rising)
            sig=cand and inzone
            res[k]['cand']+=cand; res[k]['sigdays']+=sig
            if sig and not prev[k] and (d[i]-d[last[k]]).days>=10 if last[k]>=0 else sig and not prev[k]:
                last[k]=i
                res[k]['events'].append((i,d[i]))
            prev[k]=sig
        if i==n-1:
            today=dict(date=d[i],price=c[i],gain=gain,gainpct=(peak/low-1)*100,s50=s50[i],s150=s150[i],s200=s200[i],rising=rising,belowpeak=(1-c[i]/peak)*100,inzone=inzone)
    # forward returns
    base={h:[c[i+h]/c[i]-1 for i in range(220,n-h)] for h in H}
    for k in VAR:
        res[k]['fwd']={h:[c[i+h]/c[i]-1 for i,_ in res[k]['events'] if i+h<n] for h in H}
    return dict(days=days,res=res,base=base,today=today,d0=d[220],d1=d[-1])
allr={s:run(s) for s in LIST+OTHER}
json.dump({s:{k:[str(e[1]) for e in v['res'][k]['events']] for k in VAR} for s,v in allr.items()},open(f'{D}/events.json','w'))
def table(group,name):
    print(f'\n=== {name}: {len(group)} stocks, {sum(allr[s]["days"] for s in group)/252:.0f} stock-years ===')
    print(f'{"variant":32} cand%  sig/stock-yr  ' + '  '.join(f'{h}d: mean  med  win%  p10' for h in H))
    yrs=sum(allr[s]['days'] for s in group)/252
    for k in VAR:
        cand=sum(allr[s]['res'][k]['cand'] for s in group)/sum(allr[s]['days'] for s in group)*100
        ev=sum(len(allr[s]['res'][k]['events']) for s in group)
        row=f'{k:32} {cand:5.1f}  {ev/yrs:6.2f} ({ev:4})'
        for h in H:
            f=sorted(x for s in group for x in allr[s]['res'][k]['fwd'][h])
            row+=f'   {st.mean(f)*100:5.1f} {st.median(f)*100:5.1f} {sum(x>0 for x in f)/len(f)*100:4.0f} {f[int(len(f)*.1)]*100:6.1f}'
        print(row)
    row=f'{"any day (no rule at all)":32} {"":5}  {"":13}'
    for h in H:
        f=sorted(x for s in group for x in allr[s]['base'][h])
        row+=f'   {st.mean(f)*100:5.1f} {st.median(f)*100:5.1f} {sum(x>0 for x in f)/len(f)*100:4.0f} {f[int(len(f)*.1)]*100:6.1f}'
    print(row)
table(LIST,'Starter list'); table(LIST+OTHER,'All 22 stocks')
# last 3 years only: signals per year
print('\nSignals since 2023-11 (starter list / all 22):')
for k in VAR:
    a=sum(1 for s in LIST for _,dd in allr[s]['res'][k]['events'] if str(dd)>='2023-11-16'); b=sum(1 for s in LIST+OTHER for _,dd in allr[s]['res'][k]['events'] if str(dd)>='2023-11-16')
    print(f'  {k:32} {a:4} {b:4}')
print('\nToday:')
for s in LIST+OTHER:
    t=allr[s]['today']; 
    print(f"{s:10} {t['date']} gain {t['gainpct']:6.1f}% {'Y' if t['gain'] else 'n'} | 50>150 {'Y' if t['s50']>t['s150'] else 'n'} 150>200 {'Y' if t['s150']>t['s200'] else 'n'} 50>200 {'Y' if t['s50']>t['s200'] else 'n'} p>200 {'Y' if t['price']>t['s200'] else 'n'} rising {'Y' if t['rising'] else 'n'} | below peak {t['belowpeak']:5.1f}% p>=50 {'Y' if t['price']>=t['s50'] else 'n'} zone {'Y' if t['inzone'] else 'n'} | " + ' '.join(k[0] for k,f in VAR.items() if t['gain'] and f(t['price'],t['s50'],t['s150'],t['s200'],t['rising'])))
print('\nBy period, all 22, 126d forward: n / mean / win% / p10')
for a,b in [('2000','2003'),('2003','2008'),('2008','2010'),('2010','2020'),('2020','2023'),('2023','2027')]:
    row=f'{a}-{b}: '
    for k in list(VAR)[:2]+list(VAR)[-1:]:
        f=sorted(x for s in LIST+OTHER for (i,dd),x in zip(allr[s]['res'][k]['events'],allr[s]['res'][k]['fwd'][126]) if a<=str(dd)[:4]<b)
        row+=f' {k[0]}: {len(f):4} {st.mean(f)*100:6.1f} {sum(x>0 for x in f)/len(f)*100:4.0f} {f[int(len(f)*.1)]*100:6.1f} |' if f else f' {k[0]}: 0 |'
    print(row)
