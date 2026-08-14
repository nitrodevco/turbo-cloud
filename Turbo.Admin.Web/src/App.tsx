import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from './components/RequireAuth'
import { LoginPage } from './pages/Login'
import { RoomDetailPage } from './pages/Rooms/RoomDetail'
import { RoomsListPage } from './pages/Rooms/RoomsList'
import { TerminalPage } from './pages/Terminal'
import { UserDetailPage } from './pages/Users/UserDetail'
import { UsersListPage } from './pages/Users/UsersList'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route path="/" element={<Navigate to="/users" replace />} />
        <Route path="/users" element={<UsersListPage />} />
        <Route path="/users/:id" element={<UserDetailPage />} />
        <Route path="/rooms" element={<RoomsListPage />} />
        <Route path="/rooms/:id" element={<RoomDetailPage />} />
        <Route path="/terminal" element={<TerminalPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/users" replace />} />
    </Routes>
  )
}

export default App
