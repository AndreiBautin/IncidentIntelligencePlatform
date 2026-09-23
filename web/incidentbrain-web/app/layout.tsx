import type { Metadata } from "next";
import { Toaster } from "sonner";
import "./globals.css";

export const metadata: Metadata = {
  title: "Incident Intelligence Platform",
  description: "Mini observability + AI reasoning",
};

const readOnly = process.env.NEXT_PUBLIC_READ_ONLY === "true";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="dark">
      <body className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))] antialiased">
        <Toaster theme="dark" position="top-right" richColors />
        <nav className="border-b border-[hsl(var(--border))] px-6 py-4">
          <div className="flex items-center gap-6">
            <a href="/dashboard" className="flex items-center gap-2 font-semibold text-lg tracking-tight">
              <span className="inline-block h-2 w-2 rounded-full bg-[hsl(var(--accent))] shadow-[0_0_10px_hsl(var(--accent))]" />
              Incident Intelligence Platform
            </a>
            <a href="/dashboard" className="text-sm text-zinc-400 transition-colors hover:text-white">Dashboard</a>
            {!readOnly && <a href="/settings" className="text-sm text-zinc-400 transition-colors hover:text-white">Settings</a>}
          </div>
        </nav>
        {children}
      </body>
    </html>
  );
}
