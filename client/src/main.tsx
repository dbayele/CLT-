import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import ResidentPortal from './ResidentPortal'
import './styles.css'

const path=window.location.pathname.toLowerCase()
const root=path.startsWith('/account')||path.startsWith('/my-district')
  ? <ResidentPortal/>
  : <><a href="/account" style={{position:'fixed',right:24,top:39,zIndex:40,background:'#0067a0',color:'#fff',padding:'9px 13px',borderRadius:3,textDecoration:'none',fontWeight:800,fontSize:13}}>My CLT++</a><App/></>

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>{root}</React.StrictMode>,
)
