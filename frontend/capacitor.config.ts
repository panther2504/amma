import type { CapacitorConfig } from '@capacitor/cli'

const config: CapacitorConfig = {
  appId: 'app.amma.companion',
  appName: 'Amma',
  webDir: 'dist',
  android: { backgroundColor: '#FBF6F2' },
  plugins: {
    SystemBars: { insetsHandling: 'css', initialViewportFitValueHint: 'cover', style: 'LIGHT' },
    LocalNotifications: { iconColor: '#C2497A' },
    CapacitorSQLite: { androidIsEncryption: false },
  },
}

export default config
