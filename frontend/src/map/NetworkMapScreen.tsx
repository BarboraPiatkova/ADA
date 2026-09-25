import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mapConfigQuery } from '../queries'
import { QueryState } from '../ui/QueryState'
import { MapSkeleton } from './MapSkeleton'
import { NetworkMapView } from './NetworkMapView'

/** The network map screen: base-map configuration first, then the map. */
export default function NetworkMapScreen() {
  const { t } = useTranslation()
  const mapConfig = useQuery(mapConfigQuery)
  return (
    <QueryState query={mapConfig} loading={t('app.loading')} skeleton={<MapSkeleton label={t('app.loading')} />}>
      {(config) => <NetworkMapView baseLayers={config.baseLayers} />}
    </QueryState>
  )
}
