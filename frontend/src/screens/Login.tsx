import { useState } from 'react'
import { api, setSession } from '../api/client'
import type { AuthResponse, OtpRequestResponse } from '../api/types'
import { Logo } from '../ui/Icon'
import { useToast } from '../ui/shell'

/** Passwordless sign-in: Indian mobile number (or e-mail) + one-time code. */
export function Login({ onSignedIn }: { onSignedIn: (a: AuthResponse) => void }) {
  const toast = useToast()
  const [dest, setDest] = useState('')
  const [step, setStep] = useState<'dest' | 'code'>('dest')
  const [code, setCode] = useState('')
  const [devCode, setDevCode] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const request = async () => {
    if (!dest.trim()) return toast('Enter your mobile number')
    setBusy(true)
    try {
      const r = await api<OtpRequestResponse>('/auth/otp/request', { method: 'POST', body: { destination: dest }, auth: false })
      setDevCode(r.devCode ?? null)
      if (r.devCode) setCode(r.devCode)
      setStep('code')
      toast('📩 Code sent')
    } catch (e) { toast((e as Error).message) } finally { setBusy(false) }
  }

  const verify = async () => {
    if (code.trim().length < 4) return toast('Enter the code we sent you')
    setBusy(true)
    try {
      const a = await api<AuthResponse>('/auth/otp/verify', { method: 'POST', body: { destination: dest, code }, auth: false })
      setSession(a)
      onSignedIn(a)
    } catch (e) { toast((e as Error).message) } finally { setBusy(false) }
  }

  return (
    <section id="setup" className="layer">
      <div className="scroll">
        <div className="setup-head">
          <div className="avatar" style={{ width: 52, height: 52 }}><Logo size={34} /></div>
          <h2>{step === 'dest' ? 'Welcome to Amma' : 'Enter your code'}</h2>
          <p>{step === 'dest' ? 'Sign in with your mobile number. New here? We’ll create your account.' : `We sent a 6-digit code to ${dest}.`}</p>
        </div>
        {step === 'dest' ? (
          <form onSubmit={e => { e.preventDefault(); request() }}>
            <label className="label" htmlFor="dest">Mobile number or e-mail</label>
            <input className="field" id="dest" inputMode="tel" autoComplete="tel" placeholder="+91 98765 43210" value={dest} onChange={e => setDest(e.target.value)} autoFocus />
            <p className="small muted" style={{ marginTop: 10, lineHeight: 1.5 }}>Try the demo account: <b>9999999999</b></p>
          </form>
        ) : (
          <form onSubmit={e => { e.preventDefault(); verify() }}>
            <label className="label" htmlFor="code">One-time code</label>
            <input className="field" id="code" inputMode="numeric" autoComplete="one-time-code" maxLength={8} placeholder="••••••"
              style={{ fontSize: 24, letterSpacing: 10, textAlign: 'center', fontWeight: 800 }} value={code} onChange={e => setCode(e.target.value.replace(/\D/g, ''))} autoFocus />
            {devCode && <div className="dev-code">Development mode: your code is <b>{devCode}</b> (no SMS is sent).</div>}
            <button type="button" className="small" style={{ color: 'var(--primary)', fontWeight: 700, marginTop: 14 }} onClick={() => { setStep('dest'); setCode('') }}>Change number</button>
          </form>
        )}
        <p className="small muted" style={{ marginTop: 22, lineHeight: 1.5 }}>🔒 Your health data stays private. You decide what to share with family.</p>
      </div>
      <button className="btn block" disabled={busy} onClick={step === 'dest' ? request : verify}>{busy ? 'Please wait…' : step === 'dest' ? 'Send code' : 'Verify & continue'}</button>
    </section>
  )
}
