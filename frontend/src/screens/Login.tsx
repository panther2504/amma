import { useEffect, useState } from 'react'
import { api, setSession } from '../api/client'
import type { AuthResponse, OtpRequestResponse } from '../api/types'
import { Logo } from '../ui/Icon'
import { useToast } from '../ui/shell'

type Step = 'dest' | 'code' | 'pin' | 'setup'
const LAST_DEST = 'amma.lastDest'
const pinInput = { fontSize: 24, letterSpacing: 10, textAlign: 'center', fontWeight: 800 } as const

/** Sign-in with a mobile number and either a one-time code (default) or a personal PIN (private-use mode). */
export function Login({ onSignedIn }: { onSignedIn: (a: AuthResponse) => void }) {
  const toast = useToast()
  const [mode, setMode] = useState<'otp' | 'pin' | null>(null)
  const [dest, setDest] = useState(() => { try { return localStorage.getItem(LAST_DEST) ?? '' } catch { return '' } })
  const [step, setStep] = useState<Step>('dest')
  const [code, setCode] = useState('')
  const [pin, setPin] = useState('')
  const [pin2, setPin2] = useState('')
  const [setupCode, setSetupCode] = useState('')
  const [devCode, setDevCode] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => { api<{ mode: 'otp' | 'pin' }>('/auth/config', { auth: false }).then(c => setMode(c.mode)).catch(() => setMode('otp')) }, [])

  const run = async (fn: () => Promise<void>) => { setBusy(true); try { await fn() } catch (e) { toast((e as Error).message) } finally { setBusy(false) } }
  const done = (a: AuthResponse) => { setSession(a); try { localStorage.setItem(LAST_DEST, dest) } catch { /* ignore */ } onSignedIn(a) }

  const next = () => run(async () => {
    if (!dest.trim()) { toast('Enter your mobile number'); return }
    if (mode === 'pin') {
      const r = await api<{ hasPin: boolean }>('/auth/pin/status', { method: 'POST', body: { destination: dest }, auth: false })
      setPin(''); setPin2(''); setStep(r.hasPin ? 'pin' : 'setup')
    } else {
      const r = await api<OtpRequestResponse>('/auth/otp/request', { method: 'POST', body: { destination: dest }, auth: false })
      setDevCode(r.devCode ?? null); if (r.devCode) setCode(r.devCode)
      setStep('code'); toast('📩 Code sent')
    }
  })

  const submit = () => run(async () => {
    if (step === 'code') {
      if (code.trim().length < 4) { toast('Enter the code we sent you'); return }
      done(await api<AuthResponse>('/auth/otp/verify', { method: 'POST', body: { destination: dest, code }, auth: false }))
    } else if (step === 'pin') {
      if (pin.length < 4) { toast('Enter your PIN'); return }
      try { done(await api<AuthResponse>('/auth/pin/login', { method: 'POST', body: { destination: dest, pin }, auth: false })) }
      catch (e) { setPin(''); throw e }
    } else {
      if (!/^\d{4,8}$/.test(pin)) { toast('Choose a PIN of 4 to 8 digits'); return }
      if (pin !== pin2) { toast('The two PINs do not match'); return }
      if (!setupCode.trim()) { toast('Enter the setup code'); return }
      done(await api<AuthResponse>('/auth/pin/setup', { method: 'POST', body: { destination: dest, setupCode, pin }, auth: false }))
    }
  })

  const title = step === 'dest' ? 'Welcome to Amma' : step === 'code' ? 'Enter your code' : step === 'pin' ? 'Enter your PIN' : 'Create your PIN'
  const sub = step === 'dest'
    ? (mode === 'pin' ? 'Sign in with your mobile number.' : 'Sign in with your mobile number. New here? We’ll create your account.')
    : step === 'code' ? `We sent a 6-digit code to ${dest}.`
    : step === 'pin' ? `Signing in as ${dest}.`
    : 'First time on this number. Choose a PIN you will use to sign in.'
  const digits = (v: string) => v.replace(/\D/g, '').slice(0, 8)

  return (
    <section id="setup" className="layer">
      <div className="scroll">
        <div className="setup-head">
          <div className="avatar" style={{ width: 52, height: 52 }}><Logo size={34} /></div>
          <h2>{title}</h2><p>{sub}</p>
        </div>
        <form onSubmit={e => { e.preventDefault(); if (step === 'dest') next(); else submit() }}>
          {step === 'dest' && <>
            <label className="label" htmlFor="dest">Mobile number</label>
            <input className="field" id="dest" inputMode="tel" autoComplete="tel" placeholder="+91 98765 43210" value={dest} onChange={e => setDest(e.target.value)} autoFocus />
            {mode === 'otp' && <p className="small muted" style={{ marginTop: 10, lineHeight: 1.5 }}>Try the demo account: <b>9999999999</b></p>}
          </>}
          {step === 'code' && <>
            <label className="label" htmlFor="code">One-time code</label>
            <input className="field" id="code" inputMode="numeric" autoComplete="one-time-code" maxLength={8} placeholder="••••••" style={pinInput} value={code} onChange={e => setCode(digits(e.target.value))} autoFocus />
            {devCode && <div className="dev-code">Development mode: your code is <b>{devCode}</b> (no SMS is sent).</div>}
          </>}
          {step === 'pin' && <>
            <label className="label" htmlFor="pin">PIN</label>
            <input className="field" id="pin" type="password" inputMode="numeric" autoComplete="current-password" placeholder="••••" style={pinInput} value={pin} onChange={e => setPin(digits(e.target.value))} autoFocus />
          </>}
          {step === 'setup' && <>
            <label className="label" htmlFor="newPin">New PIN (4–8 digits)</label>
            <input className="field" id="newPin" type="password" inputMode="numeric" autoComplete="new-password" placeholder="••••" style={pinInput} value={pin} onChange={e => setPin(digits(e.target.value))} autoFocus />
            <label className="label" htmlFor="newPin2">Repeat PIN</label>
            <input className="field" id="newPin2" type="password" inputMode="numeric" autoComplete="new-password" placeholder="••••" style={pinInput} value={pin2} onChange={e => setPin2(digits(e.target.value))} />
            <label className="label" htmlFor="setupCode">Setup code</label>
            <input className="field" id="setupCode" type="password" autoComplete="off" placeholder="The private code for this app" value={setupCode} onChange={e => setSetupCode(e.target.value)} />
            <p className="small muted" style={{ marginTop: 8, lineHeight: 1.5 }}>Needed only once, so nobody else can create a PIN for your number.</p>
          </>}
          {step !== 'dest' && <button type="button" className="small" style={{ color: 'var(--primary)', fontWeight: 700, marginTop: 14 }} onClick={() => { setStep('dest'); setCode(''); setPin('') }}>Change number</button>}
          <button type="submit" hidden />
        </form>
        <p className="small muted" style={{ marginTop: 22, lineHeight: 1.5 }}>🔒 Your health data stays private. You decide what to share with family.</p>
      </div>
      <button className="btn block" disabled={busy || !mode} onClick={step === 'dest' ? next : submit}>
        {busy ? 'Please wait…' : step === 'dest' ? (mode === 'pin' ? 'Continue' : 'Send code') : step === 'setup' ? 'Create PIN & continue' : step === 'pin' ? 'Sign in' : 'Verify & continue'}
      </button>
    </section>
  )
}
