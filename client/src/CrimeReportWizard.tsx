import { useMemo, useState } from 'react'
import { submitRequest, validateAddress } from './api'
import type { Draft, Service, SubmissionResult } from './types'

const steps = ['Eligibility','Report type','Reporter','Incident','People','Property','Vehicles','Evidence','Narrative','Review']
const reportTypes = ['Communicating threats','Harassing phone calls','Damage to property','Stolen property','Theft from vehicle','Lost passport / driver license']

export default function CrimeReportWizard({ service, onBack }: { service: Service; onBack: () => void }) {
  const [step,setStep]=useState(0)
  const [error,setError]=useState('')
  const [busy,setBusy]=useState(false)
  const [result,setResult]=useState<SubmissionResult|null>(null)
  const [draft,setDraft]=useState<Draft>({location:'',details:{},contact:{anonymous:false}})
  const setDetail=(k:string,v:unknown)=>setDraft(d=>({...d,details:{...d.details,[k]:v}}))
  const d=draft.details as Record<string,any>

  const canContinue=useMemo(()=>{
    if(step===0)return d.emergencyConfirmed===true && d.suspectGone===true && d.noKnownSuspectException!==false
    if(step===1)return !!d.incidentType
    if(step===2)return !!draft.contact.name?.trim() && !!draft.contact.email?.trim()
    if(step===3)return !!draft.location.trim() && d.jurisdictionConfirmed===true && !!d.occurredAt
    if(step===8)return !!String(d.narrative??'').trim()
    if(step===9)return d.certified===true
    return true
  },[step,d,draft])

  async function checkAddress(){
    if(!draft.location.trim())return setError('Enter an address first.')
    setBusy(true);setError('')
    try{
      const r=await validateAddress(draft.location)
      if(!r.valid){setDetail('jurisdictionConfirmed',false);setError(r.warning||'Address could not be validated.');return}
      setDraft(x=>({...x,location:r.normalizedAddress||x.location,details:{...x.details,jurisdictionConfirmed:true,latitude:r.latitude,longitude:r.longitude,addressWarning:r.warning||''}}))
      if(r.warning)setError(r.warning)
    }catch(e){setError(e instanceof Error?e.message:'Unable to validate address')}
    finally{setBusy(false)}
  }

  function next(){setError('');if(!canContinue){setError('Complete the required information before continuing.');return}setStep(s=>Math.min(9,s+1));window.scrollTo(0,0)}
  async function submit(){
    if(!canContinue)return setError('Review and certify the report before submitting.')
    setBusy(true);setError('')
    try{setResult(await submitRequest(service,draft))}catch(e){setError(e instanceof Error?e.message:'Unable to submit report')}finally{setBusy(false)}
  }

  if(result)return <main className="wrap wizard"><button className="link" onClick={onBack}>← Back to services</button><section className="success"><div className="success-mark">✓</div><span className="eyebrow">TEMPORARY REPORT CREATED</span><h2>{result.trackingNumber}</h2><p>Status: <b>{result.status}</b></p><div className="notice"><b>Not an official police report</b><p>{result.disclaimer}</p></div><button className="btn secondary" onClick={()=>window.print()}>Print temporary report</button></section></main>

  return <main className="wrap wizard">
    <button className="link" onClick={onBack}>← Back to services</button>
    <div className="wizard-head"><div><span className="eyebrow">POLICE · ONLINE REPORT</span><h1>Report a Crime (Non-Emergency)</h1><p>Structured online reporting with eligibility screening, temporary report number, review status, and supplements.</p></div><div className="police-mark">POLICE<br/><b>CLT++</b></div></div>
    <div className="alert"><b>Emergency? Call 911.</b><span>Do not use this form for crimes in progress, immediate danger, or a suspect who is still on scene.</span></div>
    <div className="stepper wide">{steps.map((s,i)=><div className={i<=step?'step active':'step'} key={s}><b>{i+1}</b><span>{s}</span></div>)}</div>
    <section className="panel">
      {step===0&&<><span className="eyebrow">STEP 1</span><h2>Eligibility</h2><Check checked={d.emergencyConfirmed===true} onChange={v=>setDetail('emergencyConfirmed',v)} title="This is not an emergency or crime in progress." text="Call 911 if anyone is in immediate danger or urgent police/fire/medical help is needed."/><Check checked={d.suspectGone===true} onChange={v=>setDetail('suspectGone',v)} title="The suspect is no longer on scene." text="If the suspect is present or may immediately return, call 911."/><Check checked={d.noKnownSuspectException!==false} onChange={v=>setDetail('noKnownSuspectException',v)} title="This incident is appropriate for online reporting." text="Serious violent crime, domestic violence, firearms emergencies, and other urgent matters require direct police contact."/></>}
      {step===1&&<><span className="eyebrow">STEP 2</span><h2>Choose report type</h2><div className="choice-grid">{reportTypes.map(x=><button key={x} className={d.incidentType===x?'choice active':'choice'} onClick={()=>setDetail('incidentType',x)}>{x}</button>)}</div></>}
      {step===2&&<><span className="eyebrow">STEP 3</span><h2>Reporter information</h2><Two a={<Field label="Full name *"><input value={draft.contact.name??''} onChange={e=>setDraft({...draft,contact:{...draft.contact,name:e.target.value}})}/></Field>} b={<Field label="Email *"><input type="email" value={draft.contact.email??''} onChange={e=>setDraft({...draft,contact:{...draft.contact,email:e.target.value}})}/></Field>}/><Two a={<Field label="Phone"><input value={draft.contact.phone??''} onChange={e=>setDraft({...draft,contact:{...draft.contact,phone:e.target.value}})}/></Field>} b={<Field label="Preferred contact"><select value={draft.contact.preferredMethod??''} onChange={e=>setDraft({...draft,contact:{...draft.contact,preferredMethod:e.target.value}})}><option value="">No preference</option><option>Email</option><option>Phone</option><option>Text</option></select></Field>}/>}</>}
      {step===3&&<><span className="eyebrow">STEP 4</span><h2>Incident location and time</h2><Field label="Incident address *"><div className="inline"><input value={draft.location} onChange={e=>{setDraft({...draft,location:e.target.value});setDetail('jurisdictionConfirmed',false)}} placeholder="Street address, Charlotte NC"/><button type="button" className="btn secondary" onClick={checkAddress} disabled={busy}>{busy?'Checking…':'Validate address'}</button></div></Field>{d.jurisdictionConfirmed===true&&<div className="valid">✓ Address matched by U.S. Census Geocoder</div>}<Two a={<Field label="When did it happen? *"><input type="datetime-local" value={d.occurredAt??''} onChange={e=>setDetail('occurredAt',e.target.value)}/></Field>} b={<Field label="When did you discover it?"><input type="datetime-local" value={d.discoveredAt??''} onChange={e=>setDetail('discoveredAt',e.target.value)}/></Field>}/><Field label="Location details"><input value={d.locationDetails??''} onChange={e=>setDetail('locationDetails',e.target.value)} placeholder="Apartment/unit, parking level, business name, landmark"/></Field></>}
      {step===4&&<><span className="eyebrow">STEP 5</span><h2>People</h2><p className="intro">Add victims, witnesses, suspects, or other involved people. Use one line per person if possible.</p><Field label="Victims / complainants"><textarea rows={5} value={d.victims??''} onChange={e=>setDetail('victims',e.target.value)} placeholder="Name, DOB/age, contact information, relationship to incident"/></Field><Field label="Witnesses"><textarea rows={5} value={d.witnesses??''} onChange={e=>setDetail('witnesses',e.target.value)} placeholder="Name/contact and what they observed"/></Field><Field label="Suspect information"><textarea rows={6} value={d.suspects??''} onChange={e=>setDetail('suspects',e.target.value)} placeholder="Name/alias, description, clothing, identifying marks, direction of travel"/></Field></>}
      {step===5&&<><span className="eyebrow">STEP 6</span><h2>Property</h2><Field label="Lost / stolen / damaged property"><textarea rows={7} value={d.property??''} onChange={e=>setDetail('property',e.target.value)} placeholder="Item, brand/model, serial number, color, value, ownership, damage"/></Field><Two a={<Field label="Estimated total loss"><input value={d.lossValue??''} onChange={e=>setDetail('lossValue',e.target.value)} placeholder="$0.00"/></Field>} b={<Field label="Serial / identifying numbers"><input value={d.serials??''} onChange={e=>setDetail('serials',e.target.value)}/></Field>}/>}</>}
      {step===6&&<><span className="eyebrow">STEP 7</span><h2>Vehicles</h2><Field label="Involved vehicle(s)"><textarea rows={7} value={d.vehicles??''} onChange={e=>setDetail('vehicles',e.target.value)} placeholder="Year, make, model, color, plate/state, VIN if known, owner, damage"/></Field></>}
      {step===7&&<><span className="eyebrow">STEP 8</span><h2>Evidence and attachments</h2><Field label="Evidence / cameras / digital records"><textarea rows={6} value={d.evidence??''} onChange={e=>setDetail('evidence',e.target.value)} placeholder="Security camera location, screenshots, receipts, emails/texts, photos, documents"/></Field><Field label="Attachment references"><textarea rows={4} value={d.attachments??''} onChange={e=>setDetail('attachments',e.target.value)} placeholder="Demo: list filenames or evidence references. Production would use secure uploads and malware scanning."/></Field></>}
      {step===8&&<><span className="eyebrow">STEP 9</span><h2>Incident narrative</h2><p className="intro">Describe what happened in chronological order. Include who, what, when, where, and how.</p><Field label="Narrative *"><textarea rows={12} value={d.narrative??''} onChange={e=>setDetail('narrative',e.target.value)}/></Field></>}
      {step===9&&<><span className="eyebrow">STEP 10</span><h2>Review and certify</h2><div className="summary"><Row k="Report type" v={d.incidentType}/><Row k="Reporter" v={`${draft.contact.name} · ${draft.contact.email}`}/><Row k="Location" v={draft.location}/><Row k="Occurred" v={d.occurredAt}/><Row k="Narrative" v={d.narrative}/></div><Check checked={d.certified===true} onChange={v=>setDetail('certified',v)} title="I certify this information is true and accurate to the best of my knowledge." text="Submitting false information to law enforcement may have legal consequences. This CLT++ demo does not transmit the report to CMPD."/></>}
      {error&&<div className="error">{error}</div>}
      <div className="actions">{step>0&&<button className="btn secondary" onClick={()=>setStep(s=>s-1)}>Back</button>}{step<9?<button className="btn primary" onClick={next}>Continue</button>:<button className="btn primary" disabled={busy} onClick={submit}>{busy?'Submitting…':'Submit temporary report'}</button>}</div>
    </section>
  </main>
}

function Field({label,children}:{label:string;children:any}){return <label className="field"><span>{label}</span>{children}</label>}
function Two({a,b}:{a:any;b:any}){return <div className="two">{a}{b}</div>}
function Row({k,v}:{k:string;v:any}){return <div className="row"><span>{k}</span><b>{String(v??'')}</b></div>}
function Check({checked,onChange,title,text}:{checked:boolean;onChange:(v:boolean)=>void;title:string;text:string}){return <label className="check"><input type="checkbox" checked={checked} onChange={e=>onChange(e.target.checked)}/><span><b>{title}</b><small>{text}</small></span></label>}
