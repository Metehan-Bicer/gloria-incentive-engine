import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { SessionProvider } from './auth/SessionContext'
import { Layout } from './components/Layout'
import { ImportPage } from './pages/ImportPage'
import { MyCommissionPage } from './pages/MyCommissionPage'
import { PeriodsPage } from './pages/PeriodsPage'
import { RulesPage } from './pages/RulesPage'

export default function App() {
  return (
    <SessionProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<Navigate to="/primlerim" replace />} />
            <Route path="/primlerim" element={<MyCommissionPage />} />
            <Route path="/kurallar" element={<RulesPage />} />
            <Route path="/donemler" element={<PeriodsPage />} />
            <Route path="/aktarim" element={<ImportPage />} />
            <Route path="*" element={<Navigate to="/primlerim" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </SessionProvider>
  )
}
