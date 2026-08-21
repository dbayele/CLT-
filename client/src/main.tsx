import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import ResidentPortal from './ResidentPortal'
import './styles.css'

const path=window.location.pathname.toLowerCase()
const root=path.startsWith('/account')||path.startsWith('/my-district')?<ResidentPortal/>:<App/>

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>{root}</React.StrictMode>,
)
